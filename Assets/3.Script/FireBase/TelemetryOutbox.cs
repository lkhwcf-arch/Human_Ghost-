using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HumanGhost.Telemetry
{
    // 보관 성공과 서버 저장 성공을 분리합니다. 서버 확인 전까지 원본 이벤트 ID를 보존합니다.
    public class TelemetryOutbox : ITelemetrySink
    {
        private readonly string _directory;
        private readonly long _maxBytes;
        private readonly ITelemetrySink _archive;
        public string ArchiveFailure { get; private set; }
        public event Action Pending;
        public TelemetryOutbox(string directory, long maxBytes, ITelemetrySink archive)
        {
            if (string.IsNullOrWhiteSpace(directory) || maxBytes <= 0 || archive == null)
                throw new ArgumentException("전송 대기 폴더·용량·보관 담당을 확인하세요.");
            _directory = directory; _maxBytes = maxBytes; _archive = archive;
            Directory.CreateDirectory(directory);
            // 이름 이동 직전에 종료된 정상 파일은 복원하고, 잘린 파일은 근거를 남겨 격리합니다.
            foreach (string temporary in Directory.GetFiles(directory, "*.json.tmp"))
            {
                string destination = temporary.Substring(0, temporary.Length - 4);
                try
                {
                    Read(temporary);
                    if (File.Exists(destination)) Quarantine(temporary);
                    else File.Move(temporary, destination);
                }
                catch (JsonException) { Quarantine(temporary); }
            }
        }
        public void Write(IReadOnlyList<TelemetryRecord> records)
        {
            if (records.Count == 0) return;
            string destination = Path.Combine(_directory, records[0].EventId + ".json");
            if (!File.Exists(destination))
            {
                string json = JsonConvert.SerializeObject(records);
                long size = Directory.GetFiles(_directory, "*.json", SearchOption.AllDirectories).Sum(file => new FileInfo(file).Length);
                if (size + Encoding.UTF8.GetByteCount(json) > _maxBytes) throw new IOException("미전송 기록의 보관 한도에 도달했습니다.");
                // 원격 대기열을 먼저 준비해 종료 직전에도 재실행 후 전송할 수 있게 합니다.
                string temporary = destination + ".tmp";
                File.WriteAllText(temporary, json, new UTF8Encoding(false));
                File.Move(temporary, destination);
            }
            // 원격 대기열이 확정됐다면 선택적인 로컬 열람 사본 실패가 전송을 막지 않습니다.
            try { _archive.Write(records); ArchiveFailure = null; }
            catch (Exception error) { ArchiveFailure = "로컬 열람 사본 보관 실패: " + error.Message; }
            Pending?.Invoke();
        }
        public string Next() => Directory.GetFiles(_directory, "*.json").OrderBy(file => File.GetLastWriteTimeUtc(file)).FirstOrDefault();
        public JArray Read(string path)
        {
            var records = JArray.Parse(File.ReadAllText(path));
            foreach (JToken record in records)
                if (!(record is JObject row) || !Guid.TryParseExact((string)row["EventId"], "N", out _))
                    throw new JsonSerializationException("미전송 기록의 이벤트 형식이 잘못됐습니다.");
            return records;
        }
        public void Quarantine(string path)
        {
            string rejected = Path.Combine(_directory, "Rejected");
            Directory.CreateDirectory(rejected);
            File.Move(path, Path.Combine(rejected, Guid.NewGuid().ToString("N") + ".json"));
        }
        public void Acknowledge(string path) => File.Delete(path);
    }

    // 네트워크 대기는 앱 수명의 단일 작업에서만 실행합니다. 게임 이벤트·Unity 객체를 사용하지 않습니다.
    public class TelemetryUploader : IDisposable
    {
        private readonly TelemetryOutbox _outbox;
        private readonly ITelemetryTransport _transport;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly SemaphoreSlim _signal = new SemaphoreSlim(0, 1);
        private readonly int _retryMs;
        private volatile bool _disposed;
        public string LastFailure { get; private set; }
        public Task Completion { get; }
        public TelemetryUploader(TelemetryOutbox outbox, ITelemetryTransport transport, int retryMs)
        {
            if (retryMs <= 0) throw new ArgumentOutOfRangeException(nameof(retryMs));
            _outbox = outbox; _transport = transport; _retryMs = retryMs;
            _outbox.Pending += Wake;
            Completion = RunAsync();
            Wake();
        }
        private void Wake()
        {
            try { if (!_disposed && _signal.CurrentCount == 0) _signal.Release(); }
            catch (SemaphoreFullException) { }
            catch (ObjectDisposedException) { }
        }
        private async Task RunAsync()
        {
            CancellationToken cancellation = _lifetime.Token;
            try
            {
                while (true)
                {
                    await _signal.WaitAsync(cancellation).ConfigureAwait(false);
                    int delay = _retryMs;
                    while (!cancellation.IsCancellationRequested)
                    {
                        try
                        {
                            string path = _outbox.Next();
                            if (path == null) break;
                            JArray records;
                            try { records = _outbox.Read(path); }
                            catch (JsonException)
                            {
                                _outbox.Quarantine(path);
                                LastFailure = "손상된 미전송 기록을 Rejected 폴더에 보존했습니다.";
                                continue;
                            }
                            await _transport.SendAsync(records, cancellation).ConfigureAwait(false);
                            cancellation.ThrowIfCancellationRequested();
                            _outbox.Acknowledge(path);
                            LastFailure = null; delay = _retryMs;
                        }
                        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                        catch (Exception error)
                        {
                            LastFailure = error.Message;
                            await Task.Delay(delay, cancellation).ConfigureAwait(false);
                            delay = (int)Math.Min((long)delay * 2, 60000);
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            finally { _signal.Dispose(); _lifetime.Dispose(); }
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _outbox.Pending -= Wake;
            _lifetime.Cancel();
        }
    }
}
