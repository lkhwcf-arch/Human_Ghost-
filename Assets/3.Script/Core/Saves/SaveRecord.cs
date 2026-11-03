using System;
using System.Collections.Generic;
using System.Linq;

namespace HumanGhost.Gameplay
{
    // 빈 칸도 내부 ID를 유지합니다. 화면 정렬 순번과 저장 대상은 구분합니다.
    public readonly struct SaveSlotEntry
    {
        public int Slot { get; }
        public SaveRecord Record { get; }
        public SaveSlotEntry(int slot, SaveRecord record) { Slot = slot; Record = record; }
    }

    // 위치 단위는 기존 이동 데이터와 같은 타일×100입니다. Unity 좌표 변환은 연결 계층에서 합니다.
    public sealed class SaveSnapshot
    {
        public int StageId { get; }
        public int ShrineId { get; }
        public int PositionX { get; }
        public int PositionY { get; }
        public int Hp { get; }
        public int Herbs { get; }
        public IReadOnlyList<int> ClearedStages { get; }
        public IReadOnlyList<int> ActiveShrines { get; }
        public IReadOnlyList<int> OpenedGates { get; }
        public IReadOnlyList<int> ViewedCutscenes { get; }
        public IReadOnlyList<int> UsedSavePoints { get; }

        public SaveSnapshot(int stageId, int shrineId, int positionX, int positionY, int hp, int herbs,
            IEnumerable<int> clearedStages, IEnumerable<int> activeShrines, IEnumerable<int> openedGates,
            IEnumerable<int> viewedCutscenes, IEnumerable<int> usedSavePoints)
        {
            if (stageId <= 0 || shrineId < 0 || hp < 0 || herbs < 0)
                throw new ArgumentOutOfRangeException(nameof(stageId), "저장 위치와 자원 값이 유효하지 않습니다.");
            StageId = stageId;
            ShrineId = shrineId;
            PositionX = positionX;
            PositionY = positionY;
            Hp = hp;
            Herbs = herbs;
            ClearedStages = CopyIds(clearedStages);
            ActiveShrines = CopyIds(activeShrines);
            OpenedGates = CopyIds(openedGates);
            ViewedCutscenes = CopyIds(viewedCutscenes);
            UsedSavePoints = CopyIds(usedSavePoints);
        }

        internal SaveSnapshot WithSavePoints(IEnumerable<int> savePoints) => new SaveSnapshot(
            StageId, ShrineId, PositionX, PositionY, Hp, Herbs,
            ClearedStages, ActiveShrines, OpenedGates, ViewedCutscenes, savePoints);

        private static IReadOnlyList<int> CopyIds(IEnumerable<int> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            int[] ids = source.Distinct().OrderBy(id => id).ToArray();
            if (ids.Any(id => id <= 0)) throw new ArgumentException("저장 식별자는 양수여야 합니다.", nameof(source));
            return Array.AsReadOnly(ids);
        }
    }

    public sealed class SaveRecord
    {
        public int Version { get; }
        public int Slot { get; }
        public long SavedAtUnixMs { get; }
        public long SaveOrder { get; }
        public SaveSnapshot Snapshot { get; }

        public SaveRecord(int version, int slot, long savedAtUnixMs, SaveSnapshot snapshot, long saveOrder = 0)
        {
            if (version <= 0 || slot <= 0 || savedAtUnixMs < 0
                || savedAtUnixMs > DateTimeOffset.MaxValue.ToUnixTimeMilliseconds() || saveOrder < 0)
                throw new ArgumentOutOfRangeException(nameof(version), "저장 버전·슬롯·시각이 유효하지 않습니다.");
            Version = version;
            Slot = slot;
            SavedAtUnixMs = savedAtUnixMs;
            SaveOrder = saveOrder;
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }
    }
}
