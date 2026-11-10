using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using HumanGhost.Data.Definitions;

namespace HumanGhost.Gameplay
{
    // 현재 서낭당 레벨의 비용은 외부 정책에서 공급합니다. JSON의 제안 비용을 임의로 대신 사용하지 않습니다.
    public class ShrineUpgrades : UpgradePurchases<string>
    {
        private readonly ShrineDefinitions _definitions;
        private readonly Func<int, int?> _getCost;
        private readonly Dictionary<string, int> _steps;
        public int Level { get; private set; }

        public ShrineUpgrades(ShrineDefinitions definitions, CurrencyWallet wallet,
            Func<int, int?> getCost = null, int level = 1, IReadOnlyDictionary<string, int> steps = null) : base(wallet)
        {
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            _getCost = getCost;
            if (level < 1 || level > definitions.Level.MaxLevel) throw new ArgumentOutOfRangeException(nameof(level));
            Level = level;
            _steps = definitions.Stats.Keys.ToDictionary(key => key, _ => 0, StringComparer.Ordinal);
            foreach (var pair in steps ?? new Dictionary<string, int>())
            {
                if (!definitions.Stats.TryGetValue(pair.Key, out var stat) || pair.Value < 0 || pair.Value > stat.MaxStep)
                    throw new ArgumentException("서낭당 능력치 단계가 유효하지 않습니다.", nameof(steps));
                _steps[pair.Key] = pair.Value;
            }
            if (_steps.Values.Sum(step => (long)step) != level - 1)
                throw new ArgumentException("서낭당 레벨과 구매한 능력치 단계 합계가 다릅니다.", nameof(steps));
        }

        public int GetStep(string statType) => _steps[statType];
        public IReadOnlyDictionary<string, int> CaptureSteps()
            => new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(_steps, StringComparer.Ordinal));

        protected override UpgradePurchaseResult Prepare(string statType, out PurchasePlan plan)
        {
            plan = default;
            if (statType == null || !_definitions.Stats.TryGetValue(statType, out var stat)) return UpgradePurchaseResult.UnknownTarget;
            if (!stat.Enabled) return UpgradePurchaseResult.Disabled;
            if (Level >= _definitions.Level.MaxLevel) return UpgradePurchaseResult.LevelLimit;
            if (_steps[statType] >= stat.MaxStep) return UpgradePurchaseResult.StepLimit;
            int nextStep = _steps[statType] + 1;
            int nextLevel = Level + 1;
            plan = new PurchasePlan(_definitions.Level.SoulCurrencyId, _getCost?.Invoke(Level), nextStep,
                () => { _steps[statType] = nextStep; Level = nextLevel; });
            return UpgradePurchaseResult.Purchased;
        }
    }
}
