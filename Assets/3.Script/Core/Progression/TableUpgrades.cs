using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using HumanGhost.Data.Definitions;

namespace HumanGhost.Gameplay
{
    // 엽전 약초 강화 등 Upgrade 표의 대상·단계·가격을 사용합니다. 실제 효과 적용과 화면은 소유하지 않습니다.
    public class TableUpgrades : UpgradePurchases<int>
    {
        private readonly Dictionary<int, Upgrade> _definitions;
        private readonly Dictionary<string, int> _steps;

        public TableUpgrades(IEnumerable<Upgrade> definitions, CurrencyWallet wallet,
            IReadOnlyDictionary<string, int> steps = null) : base(wallet)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            _definitions = definitions.ToDictionary(definition => definition.ID);
            _steps = _definitions.Values.Select(definition => definition.Target).Distinct(StringComparer.Ordinal)
                .ToDictionary(target => target, _ => 0, StringComparer.Ordinal);
            if (steps == null) return;
            foreach (var pair in steps)
            {
                if (!_steps.ContainsKey(pair.Key) || pair.Value < 0 || (pair.Value > 0 &&
                    !_definitions.Values.Any(definition => definition.Target == pair.Key && definition.Step == pair.Value)))
                    throw new ArgumentException("표 강화의 대상 또는 단계가 유효하지 않습니다.", nameof(steps));
                _steps[pair.Key] = pair.Value;
            }
        }

        public int GetStep(string target) => _steps[target];
        public IReadOnlyDictionary<string, int> CaptureSteps()
            => new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(_steps, StringComparer.Ordinal));

        protected override UpgradePurchaseResult Prepare(int id, out PurchasePlan plan)
        {
            plan = default;
            if (!_definitions.TryGetValue(id, out var definition)) return UpgradePurchaseResult.UnknownTarget;
            if (!definition.ContentEnabled) return UpgradePurchaseResult.Disabled;
            if (definition.Step != _steps[definition.Target] + 1) return UpgradePurchaseResult.WrongStep;
            plan = new PurchasePlan(definition.CurrencyID, definition.Price, definition.Step,
                () => _steps[definition.Target] = definition.Step);
            return UpgradePurchaseResult.Purchased;
        }
    }
}
