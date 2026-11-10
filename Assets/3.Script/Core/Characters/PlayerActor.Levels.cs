using System;
using HumanGhost.Data.Definitions;

namespace HumanGhost.Gameplay
{
    public sealed partial class PlayerActor
    {
        private CharacterStat _combatStats;
        private PlayerLevel _levelDefinition;
        public CharacterStat CombatStats => _combatStats ?? Definition;
        public int MaxHealth => CombatStats.HP;
        public int Level => _levelDefinition?.Level ?? 1;

        // 원본 대신 실행용 사본을 교체합니다. 살아 있다면 현재 체력에 최대 체력 증가분만 더합니다.
        public void ApplyLevel(PlayerLevel level)
        {
            if (ReferenceEquals(_levelDefinition, level)) return;
            if (level == null || !level.ContentEnabled) throw new ArgumentException("적용할 레벨이 활성 상태가 아닙니다.", nameof(level));
            var stats = new LevelStats(Definition, level, _critBonus, _dodgeBonus);
            int staminaMax = checked(Definition.StaminaMax + level.StaminaAdd);
            int beforeMaxHealth = MaxHealth;
            _levelDefinition = level;
            _combatStats = stats;
            if (!IsDead) Health = (int)Math.Min((long)MaxHealth, (long)Health + Math.Max(0, MaxHealth - beforeMaxHealth));
            Stamina.SetMaximum(staminaMax);
            HealthChanged?.Invoke(Health);
        }

        private class LevelStats : CharacterStat
        {
            public LevelStats(Player source, PlayerLevel level, int critBonus, int dodgeBonus)
                : base(checked(source.HP + (level?.HPAdd ?? 0)), checked(source.ATK + (level?.ATKAdd ?? 0)),
                    checked(source.DEF + (level?.DEFAdd ?? 0)), (int)Math.Max(0, Math.Min(10000L, (long)source.Dodge + dodgeBonus)),
                    (int)Math.Max(0, Math.Min(10000L, (long)source.Crit + critBonus)), source.CritDamage,
                    source.Poise, source.MoveSpeed, source.ArmorPen) { }
        }
    }
}
