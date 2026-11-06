using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace HumanGhost.Gameplay.Adapters
{
    // 폴더는 조립자가 전달합니다. 같은 폴더에는 저장소 하나만 두고 세션에서 공유합니다.
    public sealed class JsonSaveStore : ISaveStore
    {
        private readonly string _directory;
        private readonly int _version;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private readonly UTF8Encoding _encoding = new UTF8Encoding(false, true);
        private readonly JsonSerializerSettings _settings = new JsonSerializerSettings
        {
            ContractResolver = new RequiredProperties(),
            MissingMemberHandling = MissingMemberHandling.Error,
            TypeNameHandling = TypeNameHandling.None,
            MaxDepth = 16,
            Formatting = Formatting.Indented
        };

        public JsonSaveStore(string directory, int version)
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("저장 폴더 경로가 필요합니다.", nameof(directory));
            if (version <= 0) throw new ArgumentOutOfRangeException(nameof(version), "저장 버전은 양수여야 합니다.");
            _directory = Path.GetFullPath(directory);
            _version = version;
        }

        public Task<IReadOnlyList<SaveRecord>> ReadAsync() => ExecuteAsync(ReadRecords);

        private IReadOnlyList<SaveRecord> ReadRecords()
        {
            var records = new List<SaveRecord>();
            if (!Directory.Exists(_directory)) return records.AsReadOnly();
            foreach (string path in Directory.GetFiles(_directory, "slot-*.json"))
            {
                SaveRecord record = ReadRecord(path);
                if (!string.Equals(Path.GetFileName(path), GetFileName(record.Slot), StringComparison.Ordinal))
                    throw new InvalidDataException("저장 파일 이름과 내부 슬롯 식별자가 다릅니다: " + Path.GetFileName(path));
                records.Add(record);
            }
            return records.AsReadOnly();
        }

        // 확인 후 실행하는 삭제입니다. 같은 저장소의 읽기·쓰기와 동일한 잠금 안에서 최신 기록 하나만 선택합니다.
        public Task<SaveRecord> DeleteLatestAsync(CancellationToken cancellation = default) => ExecuteAsync(() =>
        {
            cancellation.ThrowIfCancellationRequested();
            SaveRecord latest = SaveRecord.LatestFirst(ReadRecords()).FirstOrDefault();
            cancellation.ThrowIfCancellationRequested();
            if (latest != null) File.Delete(Path.Combine(_directory, GetFileName(latest.Slot)));
            return latest;
        });

        public Task WriteAsync(SaveRecord record) => ExecuteAsync(() =>
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            CheckVersion(record);
            string json = JsonConvert.SerializeObject(record, _settings);
            Directory.CreateDirectory(_directory);
            string destination = Path.Combine(_directory, GetFileName(record.Slot));
            string temporary = Path.Combine(_directory, ".save-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                byte[] bytes = _encoding.GetBytes(json);
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                // 기존 파일을 먼저 삭제하지 않습니다. 교체를 지원하지 않으면 실패를 호출자에게 전달합니다.
                if (File.Exists(destination)) File.Replace(temporary, destination, null);
                else File.Move(temporary, destination);
            }
            finally { File.Delete(temporary); }
            return true;
        });

        private SaveRecord ReadRecord(string path)
        {
            try
            {
                using var input = new StringReader(_encoding.GetString(File.ReadAllBytes(path)).TrimStart('\uFEFF'));
                using var reader = new JsonTextReader(input) { MaxDepth = 16, DateParseHandling = DateParseHandling.None };
                var document = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read()) throw new InvalidDataException("저장 내용 뒤에 추가 데이터가 있습니다.");
                // 기존 버전 1 기록에는 개별 처치 목록이 없습니다. 없는 필드만 빈 목록으로 확장합니다.
                if (document["Version"]?.Type == JTokenType.Integer && (int)document["Version"] == 1
                    && document["Snapshot"] is JObject snapshot && snapshot.Property("DefeatedMonsters") == null)
                    snapshot.Add("DefeatedMonsters", new JArray());
                CheckIntegerValues(document);
                var record = document.ToObject<SaveRecord>(JsonSerializer.Create(_settings));
                CheckVersion(record);
                return record;
            }
            catch (Exception error) when (error is JsonException || error is ArgumentException
                || error is DecoderFallbackException || error is InvalidDataException || error is OverflowException)
            {
                throw new InvalidDataException("저장 파일을 불러올 수 없습니다: " + Path.GetFileName(path), error);
            }
        }

        private void CheckVersion(SaveRecord record)
        {
            if (record.Version != _version)
                throw new InvalidDataException("지원하지 않는 저장 버전입니다. 기록을 삭제하지 않고 불러오기를 중단합니다.");
        }

        // 저장 스키마의 말단 값은 모두 정수입니다. 문자열·소수를 정수로 묵시 변환하지 않습니다.
        private static void CheckIntegerValues(JToken token)
        {
            if (token is JValue && token.Type != JTokenType.Integer)
                throw new InvalidDataException("저장 데이터의 수치는 정수여야 합니다.");
            foreach (JToken child in token.Children()) CheckIntegerValues(child);
        }

        private async Task<T> ExecuteAsync<T>(Func<T> operation)
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try { return await Task.Run(operation).ConfigureAwait(false); }
            finally { _gate.Release(); }
        }

        private static string GetFileName(int slot) => "slot-" + slot.ToString(CultureInfo.InvariantCulture) + ".json";

        // 생성자와 읽기 전용 프로퍼티를 그대로 사용해 별도 JSON 모델·필드 목록의 중복을 피합니다.
        private sealed class RequiredProperties : DefaultContractResolver
        {
            protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
            {
                JsonProperty property = base.CreateProperty(member, memberSerialization);
                property.Required = Required.Always;
                return property;
            }
        }
    }
}
