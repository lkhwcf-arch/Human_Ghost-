using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HumanGhost.Gameplay
{
    public enum SaveBuildMode { Demo, Release }
    public enum SaveKind { NewGame, Manual, Automatic }

    // 구현체는 임시 파일 기록 후 교체하며, 실패 시 기존 슬롯을 보존해야 합니다.
    public interface ISaveStore
    {
        Task WriteAsync(SaveRecord record);
    }

    // 조립자가 읽어 온 기록·시계·저장소를 주입합니다. 한 실행 흐름에서 호출하며 파일·입력·화면을 소유하지 않습니다.
    public sealed class SaveSession
    {
        private readonly ISaveStore _store;
        private readonly Func<long> _utcNowMs;
        private readonly Dictionary<int, SaveRecord> _records;
        private readonly HashSet<int> _usedSavePoints = new HashSet<int>();
        private readonly SaveBuildMode _mode;
        private readonly int _version;
        private long _saveOrder;

        public int Capacity => _mode == SaveBuildMode.Demo ? 10 : 1;
        public bool ManualSaveEnabled => _mode == SaveBuildMode.Demo;
        public int? CurrentSlot { get; private set; }
        public bool IsSaving { get; private set; }
        public Task PendingWrite { get; private set; } = Task.CompletedTask;
        public event Action<SaveRecord, SaveKind> Saved;

        public SaveSession(SaveBuildMode mode, int version, ISaveStore store, Func<long> utcNowMs,
            IEnumerable<SaveRecord> records)
        {
            if (!Enum.IsDefined(typeof(SaveBuildMode), mode) || version <= 0)
                throw new ArgumentOutOfRangeException(nameof(mode), "저장 빌드 정책과 버전을 확인하세요.");
            _mode = mode;
            _version = version;
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _utcNowMs = utcNowMs ?? throw new ArgumentNullException(nameof(utcNowMs));
            _records = (records ?? throw new ArgumentNullException(nameof(records))).ToDictionary(record => record.Slot);
            if (_records.Values.Any(record => record.Slot > Capacity || record.Version != version))
                throw new ArgumentException("불러온 기록의 슬롯 범위 또는 버전이 맞지 않습니다.", nameof(records));
            _saveOrder = _records.Values.Select(record => record.SaveOrder).DefaultIfEmpty(0).Max();
        }

        // 기록이 있는 슬롯만 날짜 내림차순으로 반환합니다. 화면은 Capacity를 사용해 빈 슬롯도 표시합니다.
        public IReadOnlyList<SaveRecord> GetRecords() => SaveRecord.LatestFirst(_records.Values);

        public IReadOnlyList<SaveSlotEntry> GetSlots(bool includeEmpty)
        {
            var slots = GetRecords().Select(record => new SaveSlotEntry(record.Slot, record)).ToList();
            if (includeEmpty)
                slots.AddRange(Enumerable.Range(1, Capacity).Where(slot => !_records.ContainsKey(slot))
                    .Select(slot => new SaveSlotEntry(slot, null)));
            return slots.AsReadOnly();
        }

        public bool TryContinue(out SaveSnapshot snapshot)
        {
            snapshot = null;
            if (IsSaving || _records.Count == 0) return false;
            return TryContinue(GetRecords()[0].Slot, out snapshot);
        }

        // 테스트 화면에서 선택한 내부 슬롯을 사용합니다. 화면 정렬 순번을 슬롯 ID로 쓰지 않습니다.
        public bool TryContinue(int slot, out SaveSnapshot snapshot)
        {
            snapshot = null;
            if (IsSaving || !_records.TryGetValue(slot, out SaveRecord record)) return false;
            SetCurrent(record);
            snapshot = record.Snapshot;
            return true;
        }

        public Task<bool> NewGameAsync(SaveSnapshot initial)
        {
            if (IsSaving) return Task.FromResult(false);
            int slot = Enumerable.Range(1, Capacity).FirstOrDefault(index => !_records.ContainsKey(index));
            if (slot == 0) slot = GetRecords().Last().Slot;
            return WriteAsync(slot, initial.WithSavePoints(Array.Empty<int>()), SaveKind.NewGame);
        }

        // 덮어쓰기 확인은 화면에서 받고, 승인된 요청만 전달합니다. 빈 슬롯도 자동 선택하지 않습니다.
        public Task<bool> SaveManualAsync(int slot, bool overwriteConfirmed, SaveSnapshot snapshot)
        {
            if (!ManualSaveEnabled || IsSaving || !CurrentSlot.HasValue) return Task.FromResult(false);
            if (slot < 1 || slot > Capacity) throw new ArgumentOutOfRangeException(nameof(slot), "저장 슬롯 범위를 확인하세요.");
            if (_records.ContainsKey(slot) && !overwriteConfirmed) return Task.FromResult(false);
            return WriteAsync(slot, snapshot.WithSavePoints(_usedSavePoints), SaveKind.Manual);
        }

        public Task<bool> SaveAutomaticAsync(int savePointId, SaveSnapshot snapshot)
        {
            if (savePointId <= 0) throw new ArgumentOutOfRangeException(nameof(savePointId), "자동 저장 지점 식별자가 필요합니다.");
            if (IsSaving || !CurrentSlot.HasValue || _usedSavePoints.Contains(savePointId)) return Task.FromResult(false);
            return WriteAsync(CurrentSlot.Value, snapshot.WithSavePoints(_usedSavePoints.Concat(new[] { savePointId })), SaveKind.Automatic);
        }

        private Task<bool> WriteAsync(int slot, SaveSnapshot snapshot, SaveKind kind)
        {
            Task<bool> operation = WriteCoreAsync(slot, snapshot, kind);
            PendingWrite = operation;
            return operation;
        }

        private async Task<bool> WriteCoreAsync(int slot, SaveSnapshot snapshot, SaveKind kind)
        {
            IsSaving = true;
            try
            {
                var record = new SaveRecord(_version, slot, _utcNowMs(), snapshot, checked(_saveOrder + 1));
                await _store.WriteAsync(record);
                _records[slot] = record;
                _saveOrder = record.SaveOrder;
                SetCurrent(record);
                // 저장 성공과 소비자 오류는 구분합니다. 소비자 예외가 발생해도 성공한 기록을 되돌리지 않습니다.
                Saved?.Invoke(record, kind);
                return true;
            }
            finally { IsSaving = false; }
        }

        private void SetCurrent(SaveRecord record)
        {
            CurrentSlot = record.Slot;
            _usedSavePoints.Clear();
            _usedSavePoints.UnionWith(record.Snapshot.UsedSavePoints);
        }
    }
}
