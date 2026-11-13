using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay;
using HumanGhost.Telemetry;
using Newtonsoft.Json.Linq;

internal static class TelemetryChecks
{
    private class Sink : ITelemetrySink
    {
        public bool Fail;
        public readonly List<TelemetryRecord> Records = new List<TelemetryRecord>();
        public void Write(IReadOnlyList<TelemetryRecord> records)
        {
            if (Fail) throw new IOException("검사용 보관 실패");
            Records.AddRange(records);
        }
    }
    public static void Run(string root, Action<bool, string> check)
    {
        var config = JObject.Parse(File.ReadAllText(Path.Combine(root, "Assets/StreamingAssets/Telemetry.json")));
        var rules = new Dictionary<string, TelemetryRule>();
        foreach (string field in new[] { "capacity", "batchSize", "maxLocalBytes", "flushIntervalMs", "quitTimeoutMs" })
            check(config[field]?.Type == JTokenType.Integer && (long)config[field] > 0, "수집 정책의 양의 정수: " + field);
        check((int)config["batchSize"] <= (int)config["capacity"], "수집 묶음 크기는 메모리 한도 이하");
        check((int)config["flushIntervalMs"] <= 420000 && (int)config["quitTimeoutMs"] <= 15000,
            "예약 수집은 최대 7분·종료 대기는 유한한 범위");
        foreach (var entry in ((JObject)config["events"]).Properties())
        {
            var row = entry.Value;
            check(row["enabled"]?.Type == JTokenType.Boolean && row["version"]?.Type == JTokenType.Integer
                && (int)row["version"] > 0 && row["fields"] is JArray, "수집 정책 항목 형식: " + entry.Name);
            string[] fields = row["fields"].ToObject<string[]>();
            check(fields.All(value => !string.IsNullOrWhiteSpace(value)) && fields.Distinct().Count() == fields.Length,
                "수집 필드 이름·중복 검사: " + entry.Name);
            rules.Add(entry.Name, new TelemetryRule((bool)row["enabled"], (string)row["category"], (int)row["version"], fields));
        }
        // 실제 정책은 추가·삭제할 수 있습니다. 아래 동작 검사는 독립 예제 정책을 사용합니다.
        string[] exampleFields = { "stage_id", "currency_id", "delta", "balance", "level", "experience", "before_level", "monster_id", "placement_id", "sequence", "dead", "junction_id", "route_id", "destination_stage_id" };
        rules = new Dictionary<string, TelemetryRule>();
        foreach (string name in new[] { "stage_enter", "stage_leave", "currency_changed", "player_death", "level_up", "monster_defeated", "route_selected" })
            rules.Add(name, new TelemetryRule(true, "statistics", 1, exampleFields));
        rules.Add("item_acquired", new TelemetryRule(false, "statistics", 1, exampleFields));
        var recorder = new TelemetryRecorder(rules, 32, 8, "session", "build", "development", "data", () => 1000, () => 42);
        var source = new Dictionary<string, object> { ["currency_id"] = 1, ["delta"] = -2, ["balance"] = 3, ["secret"] = "제외" };
        check(recorder.Record("currency_changed", source), "기존 재화 기록 인수");
        source["balance"] = 900;
        var sink = new Sink { Fail = true };
        check(!recorder.Flush(sink) && recorder.PendingCount == 1 && recorder.LastFailure != null, "보관 실패 시 기록 유지·예외 격리");
        sink.Fail = false;
        check(recorder.Flush(sink) && recorder.PendingCount == 0, "보관 성공 뒤에만 제거");
        var saved = sink.Records.Single();
        check((int)saved.Fields["balance"] == 3 && !saved.Fields.ContainsKey("secret") && (int)saved.Fields["delta"] == -2,
            "허용 필드만 복사하고 음수 재화 감소량 보존");
        check(!recorder.Record("unknown", source) && !recorder.Record("item_acquired", source), "미등록·비활성 항목은 기록하지 않음");
        var definition = TestDefinitions.Player();
        var attack = new AttackSpec(1, 1, 0, 0, 0, 0, 0);
        var actor = new PlayerActor(definition, Enumerable.Repeat(attack, 5).ToArray(), true, definition.StaminaMax);
        var rewards = new MonsterRewards(new[] { new Currency(1, 1, 0, true), new Currency(2, 2, 0, true) },
            new[] { new MonsterReward(1, 3001, 40, 3, 2) },
            new[] { new PlayerLevel(1, 1, 40, 0, 0, 0, 0, true), new PlayerLevel(2, 2, 80, 10, 5, 1, 1, true) });
        using (var binding = new GameplayTelemetryBinding(recorder, actor, rewards,
            () => new Dictionary<string, object> { ["stage_id"] = 8002 }))
        {
            binding.RecordRouteChoice(0, 1, 8003);
            binding.RecordRouteChoice(1, 2, 8003);
            check(rewards.TryGrant(new MonsterSpawnId(8002, 1, 1), 3001, new GameProgressState()), "수집 구독 중에도 기존 보상 성공");
            actor.ReceiveHit(definition.HP);
            check(actor.IsDead && rewards.Wallet.GetBalance(1) == 3, "수집은 게임 상태를 변경하지 않음");
        }
        while (recorder.PendingCount > 0) recorder.Flush(sink);
        var route = sink.Records.Single(row => row.EventName == "route_selected");
        check((int)route.Fields["junction_id"] == 1 && (int)route.Fields["route_id"] == 2
            && (int)route.Fields["destination_stage_id"] == 8003, "유효한 갈림길·경로·목적지 식별자만 기록");
        check(sink.Records.Count(row => row.EventName == "player_death") == 1 &&
            sink.Records.Count(row => row.EventName == "monster_defeated") == 1 &&
            sink.Records.Count(row => row.EventName == "level_up") == 1, "기존 사망·처치·레벨 이벤트 연결");
        int pending = recorder.PendingCount;
        rewards.Wallet.TryAdd(1, 1);
        check(recorder.PendingCount == pending, "해제 이후 구독 잔존 없음");
        var closing = new TelemetryRecorder(rules, 8, 4, "closing", "build", "development", "data", () => 0, () => 0);
        closing.Record("currency_changed", source);
        closing.StopRecording();
        check(!closing.Record("currency_changed", source) && closing.PendingCount == 1, "종료 전 스냅샷 뒤 추가 기록 차단");
        var closingSink = new Sink();
        check(closing.Flush(closingSink) && closingSink.Records.Count == 1 && closing.PendingCount == 0,
            "기록을 멈춘 뒤에도 묶음 미만의 마지막 기록 보관");
        var full = new TelemetryRecorder(rules, 1, 1, "session", "build", "development", "data", () => 0, () => 0);
        full.Record("currency_changed", source);
        check(!full.Record("currency_changed", source) && full.RejectedCount == 1, "메모리 한도 초과는 게임 차단 없이 집계");
        var broken = new GameplayTelemetryBinding(full, actor, rewards, () => throw new ObjectDisposedException("검사 대상"));
        check(rewards.Wallet.TryAdd(1, 1) && broken.ContextFailureCount > 0, "수명 종료된 수집 대상도 게임 이벤트에 예외를 전파하지 않음");
        broken.Dispose();
        string temporary = Path.Combine(Path.GetTempPath(), "HumanGhostTelemetry-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new JsonTelemetryStore(temporary, 1000000);
            store.Write(sink.Records);
            var lines = File.ReadAllLines(Path.Combine(temporary, "statistics.jsonl"));
            check(lines.Length == sink.Records.Count && (string)JObject.Parse(lines[0])["EventId"] == saved.EventId,
                "로컬 UTF-8 기록과 이벤트 ID 보존");
        }
        finally { if (Directory.Exists(temporary)) Directory.Delete(temporary, true); }
    }
}
