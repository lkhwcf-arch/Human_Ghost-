using System;
using HumanGhost.Data.Definitions;

namespace HumanGhost.Gameplay
{
    public static class CombatMath
    {
        // 회피/무적은 호출자가 먼저 판정합니다. 아이템 보정은 현재 UnitStat에 이미 포함됩니다.
        public static int Damage(CharacterStat attacker, CharacterStat target, int extraDamage, bool critical)
        {
            if (attacker == null || target == null) throw new ArgumentNullException(nameof(attacker));
            long defense = Math.Max(0L, (long)target.DEF - attacker.ArmorPen);
            long damage = Math.Max(1L, (long)attacker.ATK + extraDamage - defense);
            if (critical) damage += attacker.CritDamage;
            return checked((int)damage);
        }
    }
}
