using System;
using System.Collections.Generic;

namespace HumanGhost.Gameplay
{
    // 스테이지 배치 JSON(spawn/*.json)의 읽기 전용 형태입니다. 플랫폼 배치마다 몬스터 목록을 함께 둡니다.
    public sealed class StageLayout
    {
        public int FormatVersion { get; set; }
        public int StageID { get; set; }
        public List<StagePlacement> Placements { get; set; } = new List<StagePlacement>();
    }

    public sealed class StagePlacement
    {
        public int PlacementID { get; set; }
        public int PlatformCode { get; set; }
        public string Prefab { get; set; } = "";
        public string Anchor { get; set; } = "";
        public List<StageMonsterGroup> Monsters { get; set; } = new List<StageMonsterGroup>();
    }

    public sealed class StageMonsterGroup
    {
        public int MonsterCode { get; set; }
        public int MonsterCount { get; set; }
        // FormatVersion 2: 그 줄 플랫폼에 붙는 자리, 타일×100입니다. Ground_N은 [앵커 기준 x, 바닥선 기준 높이], Air_Platform_N은 [앵커(왼쪽 끝) 기준 x, 발판 윗면 기준 높이]입니다.
        public List<List<int>> Points { get; set; } = new List<List<int>>();
        // 매복(K07 trigger): 플레이어가 이 x(맵 기준 타일×100)를 지나면 나옵니다. 없으면 바로 놓습니다.
        public int? Trigger { get; set; }
    }

    // 실제 놓을 몬스터 한 마리입니다. 처치 기록 키는 StageID·PlacementID·Sequence입니다.
    public readonly struct StageMonsterSpawn
    {
        public readonly int PlacementId, Code, Sequence, X, Height;
        public readonly int? Trigger;
        public StageMonsterSpawn(int placementId, int code, int sequence, int x, int height, int? trigger = null)
        {
            PlacementId = placementId; Code = code; Sequence = sequence; X = x; Height = height; Trigger = trigger;
        }
    }

    // 배치 해석입니다. 잘못된 항목은 경고로 모으고 그 항목만 건너뜁니다(K07).
    public static class StageLayoutRules
    {
        // Ground_N의 높이는 맵 바닥선, 그 밖의 발판은 앵커(윗면)에서 잽니다.
        public static bool UsesGroundLine(string prefab) => prefab != null && prefab.StartsWith("Ground_", StringComparison.Ordinal);

        // canSpawn: 몬스터 코드가 있고 ContentEnabled면 true입니다. 없는 코드는 경고, 끈 코드는 경고 없이 제외하도록 isKnown을 따로 받습니다.
        public static List<StageMonsterSpawn> ResolveMonsters(StageLayout layout, Func<int, bool> isKnown, Func<int, bool> isEnabled, List<string> warnings)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (isKnown == null) throw new ArgumentNullException(nameof(isKnown));
            if (isEnabled == null) throw new ArgumentNullException(nameof(isEnabled));
            if (warnings == null) throw new ArgumentNullException(nameof(warnings));
            var result = new List<StageMonsterSpawn>();
            var placementIds = new HashSet<int>();
            foreach (StagePlacement placement in layout.Placements)
            {
                if (!placementIds.Add(placement.PlacementID)) { warnings.Add("배치 번호가 중복됩니다: " + placement.PlacementID); continue; }
                int sequence = 0;
                foreach (StageMonsterGroup group in placement.Monsters)
                {
                    int count = group.Points == null ? 0 : group.Points.Count;
                    if (count != group.MonsterCount) { warnings.Add($"배치 {placement.PlacementID}: 몬스터 {group.MonsterCode} 수량 {group.MonsterCount}과 위치 {count}개가 다릅니다."); sequence += count; continue; }
                    if (!isKnown(group.MonsterCode)) { warnings.Add($"배치 {placement.PlacementID}: 없는 몬스터 코드 {group.MonsterCode}"); sequence += count; continue; }
                    if (!isEnabled(group.MonsterCode)) { sequence += count; continue; }
                    foreach (List<int> point in group.Points)
                    {
                        sequence++;
                        if (point == null || point.Count != 2) { warnings.Add($"배치 {placement.PlacementID}: 위치 형식이 잘못됐습니다."); continue; }
                        result.Add(new StageMonsterSpawn(placement.PlacementID, group.MonsterCode, sequence, point[0], point[1], group.Trigger));
                    }
                }
            }
            return result;
        }
    }

    // 출구 통과와 전멸 기록은 별개입니다. 목적지 잠금은 ContentAccess가 판단합니다.
    public static class StageExitRules
    {
        public const int VillageStage = 8001, LedgerStage = 8005;
        public static bool CanLeave(int stageId, bool ledgerCleared)
            => stageId != VillageStage || ledgerCleared;
    }
}
