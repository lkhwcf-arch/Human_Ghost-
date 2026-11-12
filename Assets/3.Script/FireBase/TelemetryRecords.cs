using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace HumanGhost.Telemetry
{
    public class TelemetryRule
    {
        public bool Enabled { get; }
        public string Category { get; }
        public int Version { get; }
        private readonly HashSet<string> _fields;
        public TelemetryRule(bool enabled, string category, int version, IEnumerable<string> fields)
        {
            if (category != "statistics" && category != "errors") throw new ArgumentException("기록 분류를 확인하세요.");
            if (version <= 0) throw new ArgumentOutOfRangeException(nameof(version));
            Enabled = enabled; Category = category; Version = version;
            _fields = new HashSet<string>(fields ?? throw new ArgumentNullException(nameof(fields)), StringComparer.Ordinal);
        }
        public bool Allows(string field) => _fields.Contains(field);
    }

    public class TelemetryRecord
    {
        public string EventId { get; }
        public string EventName { get; }
        public string Category { get; }
        public int SchemaVersion { get; }
        public string SessionId { get; }
        public string AnonymousId { get; }
        public string BuildVersion { get; }
        public string BuildMode { get; }
        public string DataVersion { get; }
        public long TimestampMs { get; }
        public long ElapsedMs { get; }
        public IReadOnlyDictionary<string, object> Fields { get; }
        public TelemetryRecord(string name, TelemetryRule rule, string sessionId, string buildVersion,
            string buildMode, string dataVersion, long timestampMs, long elapsedMs, IDictionary<string, object> fields, string anonymousId = "")
        {
            EventId = Guid.NewGuid().ToString("N"); EventName = name; Category = rule.Category;
            SchemaVersion = rule.Version; SessionId = sessionId; BuildVersion = buildVersion;
            AnonymousId = anonymousId;
            BuildMode = buildMode; DataVersion = dataVersion; TimestampMs = timestampMs; ElapsedMs = elapsedMs;
            // 허용된 기본값만 복사하므로 원본의 변경·엔진 객체 수명이 기록에 영향을 주지 않습니다.
            var copy = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var pair in fields)
                if (rule.Allows(pair.Key))
                {
                    if (!(pair.Value is int || pair.Value is long || pair.Value is bool || pair.Value is string || pair.Value == null))
                        throw new ArgumentException("수집 필드는 정수·문자열·불리언만 지원합니다: " + pair.Key);
                    copy.Add(pair.Key, pair.Value is string text && text.Length > 8192 ? text.Substring(0, 8192) : pair.Value);
                }
            Fields = new ReadOnlyDictionary<string, object>(copy);
        }
    }

    // 실패 시 같은 이벤트 ID를 유지합니다. 서버 전송 계약은 비동기 확인과 인증이 확정된 뒤 별도로 연결합니다.
    public interface ITelemetrySink { void Write(IReadOnlyList<TelemetryRecord> records); }

    public class TelemetryRecorder
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, TelemetryRule> _rules;
        private readonly Queue<TelemetryRecord> _pending = new Queue<TelemetryRecord>();
        private readonly Func<long> _utcMs, _elapsedMs;
        private readonly int _capacity, _batchSize;
        private readonly string _sessionId, _buildVersion, _buildMode, _dataVersion;
        private readonly string _anonymousId;
        private bool _flushing;
        public long RejectedCount { get; private set; }
        public string LastFailure { get; private set; }
        public int PendingCount { get { lock (_gate) return _pending.Count; } }
        public event Action BatchReady;
        public bool IsEnabled(string name) => _rules.TryGetValue(name, out var rule) && rule.Enabled;
        public TelemetryRecorder(IDictionary<string, TelemetryRule> rules, int capacity, int batchSize,
            string sessionId, string buildVersion, string buildMode, string dataVersion, Func<long> utcMs, Func<long> elapsedMs, string anonymousId = "")
        {
            if (capacity <= 0 || batchSize <= 0 || batchSize > capacity) throw new ArgumentOutOfRangeException(nameof(capacity));
            _rules = new Dictionary<string, TelemetryRule>(rules, StringComparer.Ordinal);
            _capacity = capacity; _batchSize = batchSize; _sessionId = sessionId; _buildVersion = buildVersion;
            _buildMode = buildMode; _dataVersion = dataVersion; _utcMs = utcMs; _elapsedMs = elapsedMs;
            _anonymousId = anonymousId;
        }
        public bool Record(string name, IDictionary<string, object> fields)
        {
            try
            {
                bool notify;
                lock (_gate)
                {
                    if (!_rules.TryGetValue(name, out var rule) || !rule.Enabled) return false;
                    if (_pending.Count >= _capacity) { RejectedCount++; return false; }
                    _pending.Enqueue(new TelemetryRecord(name, rule, _sessionId, _buildVersion, _buildMode,
                        _dataVersion, _utcMs(), _elapsedMs(), fields, _anonymousId));
                    notify = _pending.Count >= _batchSize || rule.Category == "errors" || name == "player_death";
                }
                if (notify) BatchReady?.Invoke();
                return true;
            }
            catch (Exception error) { LastFailure = "기록 준비 실패: " + error.Message; return false; }
        }
        public bool Flush(ITelemetrySink sink)
        {
            TelemetryRecord[] batch;
            lock (_gate)
            {
                if (_flushing) return false;
                if (_pending.Count == 0) return true;
                _flushing = true; batch = _pending.Take(_batchSize).ToArray();
            }
            try
            {
                sink.Write(Array.AsReadOnly(batch));
                lock (_gate) for (int i = 0; i < batch.Length; i++) _pending.Dequeue();
                LastFailure = null; return true;
            }
            catch (Exception error) { LastFailure = "기록 보관 실패: " + error.Message; return false; }
            finally { lock (_gate) _flushing = false; }
        }
    }
}
