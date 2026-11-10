using System;

namespace HumanGhost.Gameplay
{
    public sealed partial class PlayerActor
    {
        private int _critBonus;
        private int _dodgeBonus;
        private int _parryCostDelta;
        private int _baseHerbHeal;
        private int _baseHerbCharges;
        public int HerbCapacity => _herbs?.Capacity ?? 0;
        public int HerbHealing => _herbs?.Heal ?? 0;

        public void ApplyUpgrades(UpgradeSession upgrades)
        {
            if (upgrades == null) throw new ArgumentNullException(nameof(upgrades));
            _critBonus = upgrades.GetShrineValue("Crit");
            _dodgeBonus = upgrades.GetShrineValue("Dodge");
            _parryCostDelta = upgrades.GetShrineValue("ParryStamina");
            _combatStats = new LevelStats(Definition, _levelDefinition, _critBonus, _dodgeBonus);
            Stamina.SetRegenBonus(upgrades.GetShrineValue("StaminaRegen"));
            _combo.SetTimeReduction(upgrades.GetShrineValue("AttackSpeed"));
            if (_herbs != null) _herbs.Configure(checked(_baseHerbHeal + upgrades.GetTableValue("HERB_HEAL")),
                checked(_baseHerbCharges + upgrades.GetTableValue("HERB_CHARGES")));
            NotifyDisplayChanged();
        }
    }
}
