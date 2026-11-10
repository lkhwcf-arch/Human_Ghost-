using System;

namespace HumanGhost.Gameplay
{
    // 확률 타격 결과. None은 비판정·사망·무적·중복 대상 등으로 판정 자체가 없었음을 뜻합니다.
    public enum HitOutcome
    {
        None,
        Dodged,
        Parried,
        Hit,
        Critical
    }

    // 유효 접촉 한 번의 창 등록→상대 회피→공격자 치명 순서를 한곳에서 조정합니다. 피해 적용은 Actor 책임입니다.
    internal static class HitRoll
    {
        // 확률 단위(10000 = 100%). 밸런스 수치가 아닙니다.
        private const int _probabilityScale = 10000;

        // random(minInclusive, maxExclusive): 공격자 개체가 소유한 난수(0 이상 10000 미만). 확률은 0~10000. null이면 호출자가 준 dodged/critical을 그대로 씁니다.
        public static HitOutcome Resolve(AttackWindow window, int targetId, int dodge, int crit, bool blocked, bool canCrit,
            Func<int, int, int> random, bool dodged, bool critical)
        {
            if (targetId <= 0) throw new ArgumentOutOfRangeException(nameof(targetId));
            if (random != null) { Validate(dodge, nameof(dodge)); Validate(crit, nameof(crit)); }
            // 비판정·사망·무적·중복 대상은 난수를 소비하지 않습니다. 회피한 대상도 이 창의 등록은 소비합니다.
            if (blocked || !window.TryRegisterTarget(targetId)) return HitOutcome.None;
            if (random == null) return dodged ? HitOutcome.Dodged : critical && canCrit ? HitOutcome.Critical : HitOutcome.Hit;
            if (Succeeds(dodge, random)) return HitOutcome.Dodged;
            return canCrit && Succeeds(crit, random) ? HitOutcome.Critical : HitOutcome.Hit;
        }

        private static bool Succeeds(int probability, Func<int, int, int> random)
        {
            int value = random(0, _probabilityScale);
            if (value < 0 || value >= _probabilityScale) throw new InvalidOperationException("확률 난수 공급 범위를 벗어났습니다.");
            return value < probability;
        }

        private static void Validate(int probability, string name)
        {
            if (probability < 0 || probability > _probabilityScale) throw new ArgumentOutOfRangeException(name, "확률은 0~10000입니다.");
        }
    }
}
