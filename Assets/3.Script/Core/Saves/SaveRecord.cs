using System;
using System.Collections.Generic;
using System.Linq;

namespace HumanGhost.Gameplay
{
    // 배치 순번은 같은 플랫폼의 몬스터 목록을 펼친 1부터 시작하는 번호입니다.
    public readonly struct MonsterSpawnId : IEquatable<MonsterSpawnId>
    {
        public int StageId { get; }
        public int PlacementId { get; }
        public int Sequence { get; }
        public MonsterSpawnId(int stageId, int placementId, int sequence)
        {
            if (stageId <= 0 || placementId <= 0 || sequence <= 0)
                throw new ArgumentOutOfRangeException(nameof(stageId), "몬스터 배치 식별자는 양수여야 합니다.");
            StageId = stageId; PlacementId = placementId; Sequence = sequence;
        }
        public bool Equals(MonsterSpawnId other) => StageId == other.StageId && PlacementId == other.PlacementId && Sequence == other.Sequence;
        public override bool Equals(object obj) => obj is MonsterSpawnId other && Equals(other);
        public override int GetHashCode() => unchecked((StageId * 397 ^ PlacementId) * 397 ^ Sequence);
    }

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
        public IReadOnlyList<MonsterSpawnId> DefeatedMonsters { get; }
        public RewardSnapshot Rewards { get; }
        public UpgradeSnapshot Upgrades { get; }
        public GalimgilSnapshot Galimgil { get; }

        public SaveSnapshot(int stageId, int shrineId, int positionX, int positionY, int hp, int herbs,
            IEnumerable<int> clearedStages, IEnumerable<int> activeShrines, IEnumerable<int> openedGates,
            IEnumerable<int> viewedCutscenes, IEnumerable<int> usedSavePoints, IEnumerable<MonsterSpawnId> defeatedMonsters = null,
            RewardSnapshot rewards = null, UpgradeSnapshot upgrades = null, GalimgilSnapshot galimgil = null)
        {
            if (stageId <= 0 || shrineId < 0 || hp < 0 || herbs < 0)
                throw new ArgumentOutOfRangeException(nameof(stageId), "저장 위치와 자원 값이 유효하지 않습니다.");
            StageId = stageId;
            ShrineId = shrineId;
            PositionX = positionX;
            PositionY = positionY;
            Hp = hp;
            Herbs = herbs;
            Rewards = rewards ?? new RewardSnapshot();
            Upgrades = upgrades ?? new UpgradeSnapshot();
            Galimgil = galimgil ?? new GalimgilSnapshot();
            ClearedStages = CopyIds(clearedStages);
            ActiveShrines = CopyIds(activeShrines);
            OpenedGates = CopyIds(openedGates);
            ViewedCutscenes = CopyIds(viewedCutscenes);
            UsedSavePoints = CopyIds(usedSavePoints);
            MonsterSpawnId[] defeated = (defeatedMonsters ?? Array.Empty<MonsterSpawnId>()).Distinct()
                .OrderBy(id => id.StageId).ThenBy(id => id.PlacementId).ThenBy(id => id.Sequence).ToArray();
            if (defeated.Any(id => id.StageId <= 0 || id.PlacementId <= 0 || id.Sequence <= 0))
                throw new ArgumentException("몬스터 배치 식별자는 양수여야 합니다.", nameof(defeatedMonsters));
            DefeatedMonsters = Array.AsReadOnly(defeated);
        }

        internal SaveSnapshot WithSavePoints(IEnumerable<int> savePoints) => Copy(savePoints, OpenedGates);
        internal SaveSnapshot WithOpenedGates(IEnumerable<int> openedGates) => Copy(UsedSavePoints, openedGates);
        private SaveSnapshot Copy(IEnumerable<int> savePoints, IEnumerable<int> openedGates) => new SaveSnapshot(
            StageId, ShrineId, PositionX, PositionY, Hp, Herbs,
            ClearedStages, ActiveShrines, openedGates, ViewedCutscenes, savePoints, DefeatedMonsters, Rewards, Upgrades, Galimgil);

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
        // 화면·이어하기·삭제가 동일한 최신 기록 기준을 사용합니다.
        public static IReadOnlyList<SaveRecord> LatestFirst(IEnumerable<SaveRecord> records) => Array.AsReadOnly(
            records.OrderByDescending(record => record.SavedAtUnixMs).ThenByDescending(record => record.SaveOrder)
                .ThenBy(record => record.Slot).ToArray());
        public int Version { get; }
        public int Slot { get; }
        public long SavedAtUnixMs { get; }
        public long SaveOrder { get; }
        public SaveSnapshot Snapshot { get; }
        public SaveRecord WithExistingGates(Func<int, bool> gateExists)
        {
            if (gateExists == null) throw new ArgumentNullException(nameof(gateExists));
            return new SaveRecord(Version, Slot, SavedAtUnixMs, Snapshot.WithOpenedGates(Snapshot.OpenedGates.Where(gateExists)), SaveOrder);
        }

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
