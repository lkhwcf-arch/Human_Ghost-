using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace HumanGhost.Data.Definitions
{
    // 외부 JSON의 읽기 전용 정의입니다. 보유 재화·선택 단계·스탯 원본은 변경하지 않습니다.
    public class ShrineDefinitions
    {
        public ShrineLevelDefinition Level { get; }
        public IReadOnlyDictionary<string, ShrineStatDefinition> Stats { get; }

        public ShrineDefinitions(ShrineLevelDefinition level, IEnumerable<ShrineStatDefinition> stats)
        {
            Level = level;
            Stats = new ReadOnlyDictionary<string, ShrineStatDefinition>(
                stats.ToDictionary(stat => stat.StatType, System.StringComparer.Ordinal));
        }

        internal static IReadOnlyDictionary<int, int> CopySteps(IEnumerable<KeyValuePair<int, int>> steps)
            => new ReadOnlyDictionary<int, int>(steps.ToDictionary(pair => pair.Key, pair => pair.Value));
    }

    public class ShrineLevelDefinition
    {
        public int MaxLevel { get; }
        public int SoulCurrencyId { get; }
        public int DeathSoulLossRate { get; }
        public int SoulCostBase { get; }
        public int SoulCostRate { get; }
        // 키는 현재 레벨이며 값은 다음 레벨로 가는 비용입니다.
        public IReadOnlyDictionary<int, int> SoulCosts { get; }

        public ShrineLevelDefinition(int maxLevel, int soulCurrencyId, int deathSoulLossRate, int soulCostBase, int soulCostRate,
            IEnumerable<KeyValuePair<int, int>> soulCosts)
        {
            MaxLevel = maxLevel; SoulCurrencyId = soulCurrencyId; DeathSoulLossRate = deathSoulLossRate;
            SoulCostBase = soulCostBase; SoulCostRate = soulCostRate;
            SoulCosts = ShrineDefinitions.CopySteps(soulCosts);
        }
    }

    public class ShrineStatDefinition
    {
        public string StatType { get; }
        public int TextId { get; }
        public string Target { get; }
        public bool Enabled { get; }
        public int MaxStep { get; }
        // 단계마다 더하는 증감값이며 누적 합계는 실행 규칙에서 계산합니다.
        public IReadOnlyDictionary<int, int> Steps { get; }

        public ShrineStatDefinition(string statType, int textId, string target, bool enabled, int maxStep,
            IEnumerable<KeyValuePair<int, int>> steps)
        {
            StatType = statType; TextId = textId; Target = target; Enabled = enabled; MaxStep = maxStep;
            Steps = ShrineDefinitions.CopySteps(steps);
        }
    }
}
