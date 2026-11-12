using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HumanGhost.Telemetry
{
    // 앱 수명만 소유합니다. 게임 이벤트에서는 기록만 쌓고 보관은 별도 예약으로 실행합니다.
    public class TelemetryHost : MonoBehaviour
    {
        public static TelemetryHost Current { get; private set; }
        public TelemetryRecorder Recorder { get; private set; }
        public string LastFailure { get; private set; }
        private ITelemetrySink _sink;
        private SynchronizationContext _context;
        private readonly System.Diagnostics.Stopwatch _clock = new System.Diagnostics.Stopwatch();
        private int _scheduled;
        private volatile bool _active;
        private volatile string _sceneName = "";
        private TelemetryUploader _uploader;
        private HttpClient _http;
        public string UploadFailure => _uploader?.LastFailure;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Current = null;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void StartSession()
        {
            if (Current != null) return;
            var root = new GameObject("Telemetry");
            DontDestroyOnLoad(root);
            root.AddComponent<TelemetryHost>();
        }
        private void Awake()
        {
            Current = this;
            try
            {
                var config = JObject.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Telemetry.json")));
                if (!(bool)config["enabled"] || (Application.isEditor && !(bool)config["includeEditor"])) return;
                var rules = new Dictionary<string, TelemetryRule>(StringComparer.Ordinal);
                foreach (var property in ((JObject)config["events"]).Properties())
                {
                    var row = property.Value;
                    rules.Add(property.Name, new TelemetryRule((bool)row["enabled"], (string)row["category"],
                        (int)row["version"], row["fields"].ToObject<string[]>()));
                }
                string sessionId = Guid.NewGuid().ToString("N");
                _context = SynchronizationContext.Current;
                if (_context == null) throw new InvalidOperationException("수집 예약을 위한 실행 문맥이 없습니다.");
                _clock.Start();
                Recorder = new TelemetryRecorder(rules, (int)config["capacity"], (int)config["batchSize"],
                    sessionId, Application.version, Debug.isDebugBuild ? "development" : "release",
                    (string)config["dataVersion"], () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), () => _clock.ElapsedMilliseconds,
                    ReadAnonymousId());
                string directory = Path.Combine(Application.persistentDataPath, "Telemetry");
                _sink = new JsonTelemetryStore(directory, (long)config["maxLocalBytes"]);
                var firebase = (JObject)config["firebase"];
                if (firebase != null && (bool)firebase["enabled"])
                {
                    _http = new HttpClient { Timeout = TimeSpan.FromMilliseconds((int)firebase["timeoutMs"]) };
                    var outbox = new TelemetryOutbox(Path.Combine(directory, "Pending"), (long)firebase["maxPendingBytes"], _sink);
                    _sink = outbox;
                    _uploader = new TelemetryUploader(outbox,
                        new FirebaseRestTransport(_http, (string)firebase["projectId"], (string)firebase["apiKey"]), (int)firebase["retryMs"]);
                }
                _active = true;
                Recorder.BatchReady += ScheduleFlush;
                Application.logMessageReceivedThreaded += OnLog;
                SceneManager.sceneLoaded += OnSceneLoaded;
                SceneManager.sceneUnloaded += OnSceneUnloaded;
                Record("session_start", new Dictionary<string, object>());
            }
            catch (Exception error)
            {
                Release();
                Recorder = null; _sink = null;
                LastFailure = "수집 초기화 실패: " + error.Message;
            }
        }
        public void Record(string name, IDictionary<string, object> fields) => Recorder?.Record(name, fields);
        private void OnLog(string message, string stack, LogType type)
        {
            if (!_active || (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)) return;
            Record("game_error", new Dictionary<string, object> { ["kind"] = type.ToString(), ["message"] = message, ["stack"] = stack, ["scene"] = _sceneName });
        }
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _sceneName = scene.name;
            Record("scene_enter", new Dictionary<string, object> { ["scene"] = scene.name });
        }
        private void OnSceneUnloaded(Scene scene)
        {
            Record("scene_leave", new Dictionary<string, object> { ["scene"] = scene.name });
            ScheduleFlush();
        }
        private static string ReadAnonymousId()
        {
            string directory = Path.Combine(Application.persistentDataPath, "Telemetry");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "anonymous-id.txt");
            if (File.Exists(path) && Guid.TryParse(File.ReadAllText(path), out var previous)) return previous.ToString("N");
            string id = Guid.NewGuid().ToString("N");
            File.WriteAllText(path, id);
            return id;
        }
        private void ScheduleFlush()
        {
            if (Interlocked.Exchange(ref _scheduled, 1) != 0) return;
            try { _context.Post(_ =>
            {
                Interlocked.Exchange(ref _scheduled, 0);
                if (this == null || !_active) return;
                Flush();
            }, null); }
            catch (Exception error)
            {
                Interlocked.Exchange(ref _scheduled, 0);
                LastFailure = "수집 보관 예약 실패: " + error.Message;
            }
        }
        public void Flush()
        {
            if (!_active || Recorder == null) return;
            if (!Recorder.Flush(_sink)) { LastFailure = Recorder.LastFailure; return; }
            LastFailure = null;
            if (Recorder.PendingCount > 0) ScheduleFlush();
        }
        private void OnApplicationPause(bool paused) { if (paused) Flush(); }
        private void OnApplicationQuit()
        {
            Record("session_end", new Dictionary<string, object>());
            // 정상 종료 시 남은 메모리 기록만 비웁니다. 실패 시 무한 재시도하지 않습니다.
            while (Recorder != null && Recorder.PendingCount > 0 && Recorder.Flush(_sink)) { }
        }
        private void Release()
        {
            _active = false;
            _uploader?.Dispose();
            _http?.Dispose();
            if (Recorder != null) Recorder.BatchReady -= ScheduleFlush;
            Application.logMessageReceivedThreaded -= OnLog;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            _clock.Stop();
        }
        private void OnDestroy()
        {
            Release();
            if (Current == this) Current = null;
        }
    }
}
