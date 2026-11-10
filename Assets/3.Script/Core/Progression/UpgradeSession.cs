using System;
using System.Collections.Generic;
using System.Linq;
using HumanGhost.Data.Definitions;

namespace HumanGhost.Gameplay
{
    // 두 구매 규칙과 효과 조회만 조립합니다. 입력·화면·파일·플레이어 개체는 소유하지 않습니다.
    public class UpgradeSession
    {
        private readonly CurrencyWallet _wallet;
        private readonly ShrineDefinitions _definitions;
        private readonly Upgrade[] _upgrades;
        private ShrineUpgrades _shrine;
        private TableUpgrades _table;
        public int ShrineLevel => _shrine.Level;
        public event Action EffectsChanged;
        public UpgradeSession(CurrencyWallet wallet, ShrineDefinitions definitions, IEnumerable<Upgrade> upgrades)
        {
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            if (upgrades == null) throw new ArgumentNullException(nameof(upgrades));
            _upgrades = upgrades.ToArray();
            Restore(new UpgradeSnapshot());
        }
        public UpgradePurchaseResult BuyShrine(string statType)
            => statType is "Crit" or "Dodge" or "AttackSpeed" or "StaminaRegen" or "ParryStamina"
                ? _shrine.Buy(statType) : UpgradePurchaseResult.UnknownTarget;
        public UpgradePurchaseResult BuyUpgrade(int id)
            => _upgrades.Any(row => row.ID == id && (row.Target == "HERB_CHARGES" || row.Target == "HERB_HEAL"))
                ? _table.Buy(id) : UpgradePurchaseResult.UnknownTarget;
        public UpgradeSnapshot Capture() => new UpgradeSnapshot(_shrine.Level, _shrine.CaptureSteps(), _table.CaptureSteps());
        public void Restore(UpgradeSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (_wallet.IsChanging) throw new InvalidOperationException("구매 통지 중에는 강화 상태를 복원할 수 없습니다.");
            var cost = new ShrineUpgradeCost(_definitions.Level);
            var shrine = new ShrineUpgrades(_definitions, _wallet, cost.GetCost, snapshot.ShrineLevel, snapshot.ShrineSteps);
            var table = new TableUpgrades(_upgrades, _wallet, snapshot.TableSteps);
            shrine.Purchased += (key, step) => EffectsChanged?.Invoke();
            table.Purchased += (id, step) => EffectsChanged?.Invoke();
            _shrine = shrine;
            _table = table;
        }
        public int GetShrineValue(string key)
        {
            if (!_definitions.Stats.TryGetValue(key, out var stat)) return 0;
            int step = _shrine.GetStep(key);
            return checked((int)stat.Steps.Where(pair => pair.Key <= step).Sum(pair => (long)pair.Value));
        }
        public int GetTableValue(string target)
        {
            var rows = _upgrades.Where(row => row.Target == target).ToArray();
            if (rows.Length == 0) return 0;
            int step = _table.GetStep(target);
            return checked((int)rows.Where(row => row.Step <= step).Sum(row => (long)row.Value));
        }
    }
}
