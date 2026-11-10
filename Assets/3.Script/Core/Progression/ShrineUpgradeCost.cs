using System;
using System.Numerics;
using HumanGhost.Data.Definitions;

namespace HumanGhost.Gameplay
{
    // 현재 레벨의 비용을 기본값에서 직접 계산하고 마지막 나눗셈에서만 버립니다.
    public class ShrineUpgradeCost
    {
        private const int _rateDenominator = 10000;
        private readonly ShrineLevelDefinition _definition;
        public ShrineUpgradeCost(ShrineLevelDefinition definition)
            => _definition = definition ?? throw new ArgumentNullException(nameof(definition));

        public int? GetCost(int level)
        {
            if (level < 1 || level >= _definition.MaxLevel) return null;
            int exponent = level - 1;
            BigInteger numerator = (BigInteger)_definition.SoulCostBase * BigInteger.Pow(_definition.SoulCostRate, exponent);
            BigInteger cost = numerator / BigInteger.Pow(_rateDenominator, exponent);
            return cost < 0 || cost > int.MaxValue ? (int?)null : (int)cost;
        }
    }
}
