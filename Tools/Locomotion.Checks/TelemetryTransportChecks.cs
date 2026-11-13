using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using HumanGhost.Telemetry;
using Newtonsoft.Json.Linq;

internal static class TelemetryTransportChecks
{
    private class Handler : HttpMessageHandler
    {
        public int SignIns, Refreshes, Writes;
        public bool Conflict, Unauthorized;
        public Action<JObject> Inspect;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            string url = request.RequestUri.AbsoluteUri;
            if (url.StartsWith("https://identitytoolkit.googleapis.com/", StringComparison.Ordinal))
            {
                SignIns++;
                return Json(HttpStatusCode.OK, "{\"idToken\":\"token\",\"refreshToken\":\"refresh\",\"localId\":\"uid\",\"expiresIn\":\"3600\"}");
            }
            if (url.StartsWith("https://securetoken.googleapis.com/", StringComparison.Ordinal))
            {
                Refreshes++;
                return Json(HttpStatusCode.OK, "{\"id_token\":\"renewed\",\"refresh_token\":\"refresh\",\"user_id\":\"uid\",\"expires_in\":\"3600\"}");
            }
            Writes++;
            Inspect?.Invoke(JObject.Parse(await request.Content.ReadAsStringAsync()));
            if (Unauthorized) { Unauthorized = false; return Json(HttpStatusCode.Unauthorized, "{}"); }
            return Conflict ? Json(HttpStatusCode.Conflict, "{\"error\":{\"status\":\"ALREADY_EXISTS\"}}") : Json(HttpStatusCode.OK, "{}");
        }
        private static HttpResponseMessage Json(HttpStatusCode code, string value) => new HttpResponseMessage(code) { Content = new StringContent(value) };
    }
    private class WaitingTransport : ITelemetryTransport
    {
        public readonly TaskCompletionSource<bool> Started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task SendAsync(JArray records, CancellationToken cancellation)
        {
            Started.TrySetResult(true);
            await Task.Delay(Timeout.Infinite, cancellation);
        }
    }
    private class BrokenArchive : ITelemetrySink
    {
        public void Write(IReadOnlyList<TelemetryRecord> records) => throw new IOException("검사용 열람 사본 실패");
    }
    public static async Task RunAsync(Action<bool, string> check)
    {
        var rule = new TelemetryRule(true, "statistics", 1, new[] { "delta", "label" });
        var record = new TelemetryRecord("currency_changed", rule, Guid.NewGuid().ToString("N"), "test", "development", "data", 1000, 0,
            new Dictionary<string, object> { ["delta"] = -2L, ["label"] = "따옴표 \" 줄바꿈\n" }, Guid.NewGuid().ToString("N"));
        var handler = new Handler { Conflict = true, Unauthorized = true };
        handler.Inspect = body => check((string)body["fields"]?["Fields"]?["mapValue"]?["fields"]?["delta"]?["integerValue"] == "-2"
            && (string)body["fields"]?["OwnerUid"]?["stringValue"] == "uid", "Firestore 정수·소유 UID 인코딩");
        using (var http = new HttpClient(handler))
        {
            var transport = new FirebaseRestTransport(http, "검사프로젝트", "공개키검사값");
            await transport.SendAsync(new JArray(JObject.FromObject(record)), CancellationToken.None);
            check(handler.SignIns == 1 && handler.Refreshes == 1 && handler.Writes == 2, "인증 만료 후 토큰 갱신·중복 기록 확인");
        }
        string directory = Path.Combine(Path.GetTempPath(), "HumanGhostOutbox-" + Guid.NewGuid().ToString("N"));
        try
        {
            var outbox = new TelemetryOutbox(Path.Combine(directory, "Pending"), 1000000, new JsonTelemetryStore(directory, 1000000));
            outbox.Write(new[] { record });
            string path = outbox.Next();
            check(path != null && (string)outbox.Read(path)[0]["EventId"] == record.EventId, "디스크 대기열에 원래 이벤트 ID 보존");
            var restored = new TelemetryOutbox(Path.Combine(directory, "Pending"), 1000000, new JsonTelemetryStore(directory, 1000000));
            check(restored.Next() == path, "재실행 후 미전송 묶음 복원");
            var waiting = new WaitingTransport();
            var uploader = new TelemetryUploader(restored, waiting, 1);
            await waiting.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Task finishing = uploader.FinishAsync();
            check(!finishing.IsCompleted, "서버 확인 전 종료 전송 완료로 처리하지 않음");
            uploader.Dispose();
            await finishing.WaitAsync(TimeSpan.FromSeconds(5));
            check(File.Exists(path), "종료 취소 중 서버 확인 없는 파일은 제거하지 않음");
            restored.Acknowledge(path);
            check(restored.Next() == null, "서버 확인 뒤에만 대기열 제거");
            restored.Write(new[] { record });
            using (var http = new HttpClient(new Handler()))
            {
                var successful = new TelemetryUploader(restored, new FirebaseRestTransport(http, "검사프로젝트", "검사공개키"), 1);
                await successful.FinishAsync().WaitAsync(TimeSpan.FromSeconds(5));
                check(restored.Next() == null, "종료 대기 중 서버 확인을 받은 파일만 정리");
                await successful.FinishAsync();
                successful.Dispose();
            }
            var withoutArchive = new TelemetryOutbox(Path.Combine(directory, "ArchiveFailure"), 1000000, new BrokenArchive());
            withoutArchive.Write(new[] { record });
            check(withoutArchive.ArchiveFailure != null && withoutArchive.Next() != null,
                "열람 사본이 실패해도 영속 전송 대기열은 유지");
            string broken = Path.Combine(directory, "Pending", "broken.json");
            File.WriteAllText(broken, "{broken");
            restored.Quarantine(broken);
            check(restored.Next() == null && Directory.GetFiles(Path.Combine(directory, "Pending", "Rejected")).Length == 1,
                "손상 기록을 삭제하지 않고 격리해 다음 전송을 막지 않음");
            string recovery = Path.Combine(directory, "Recovery"); Directory.CreateDirectory(recovery);
            File.WriteAllText(Path.Combine(recovery, "complete.json.tmp"), new JArray(JObject.FromObject(record)).ToString());
            File.WriteAllText(Path.Combine(recovery, "partial.json.tmp"), "{broken");
            var recovered = new TelemetryOutbox(recovery, 1000000, new JsonTelemetryStore(directory, 1000000));
            check(recovered.Next() != null && Directory.GetFiles(Path.Combine(recovery, "Rejected")).Length == 1,
                "종료 직전 임시 파일의 정상 기록은 복원하고 불완전 기록은 보존 격리");
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
