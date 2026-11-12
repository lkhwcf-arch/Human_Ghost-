using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace HumanGhost.Telemetry
{
    // 기존 시험용 전송기를 로컬 보관 경계로 교체했습니다. 서버 인증·전송은 아직 연결하지 않습니다.
    public class JsonTelemetryStore : ITelemetrySink
    {
        private readonly string _directory;
        private readonly long _maxBytes;
        public JsonTelemetryStore(string directory, long maxBytes)
        {
            _directory = directory ?? throw new ArgumentNullException(nameof(directory));
            if (maxBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxBytes));
            _maxBytes = maxBytes;
        }
        public void Write(IReadOnlyList<TelemetryRecord> records)
        {
            Directory.CreateDirectory(_directory);
            long existing = 0;
            foreach (string file in Directory.GetFiles(_directory, "*.jsonl")) existing += new FileInfo(file).Length;
            var files = new Dictionary<string, StringBuilder>();
            long bytes = 0;
            foreach (var record in records)
            {
                string json = JsonConvert.SerializeObject(record) + "\n";
                bytes += Encoding.UTF8.GetByteCount(json);
                if (!files.TryGetValue(record.Category, out var content)) files.Add(record.Category, content = new StringBuilder());
                content.Append(json);
            }
            if (existing + bytes > _maxBytes) throw new IOException("수집 기록 보관 한도에 도달했습니다.");
            // 부분 쓰기 뒤 재시도는 같은 이벤트 ID를 유지합니다. 집계에서 중복을 제거합니다.
            foreach (var file in files) File.AppendAllText(Path.Combine(_directory, file.Key + ".jsonl"), file.Value.ToString(), new UTF8Encoding(false));
        }
    }
}
