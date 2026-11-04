using System;

namespace HumanGhost.Gameplay
{
    // 전투 중 여부(K12·O11)입니다. 발견이나 피격 뒤 일정 시간 관측이 없으면 끝납니다.
    public sealed class CombatPresence
    {
        private readonly int _holdMilliseconds;
        private int _remaining;
        public bool InCombat => _remaining > 0;

        public CombatPresence(int holdMilliseconds)
        {
            if (holdMilliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(holdMilliseconds));
            _holdMilliseconds = holdMilliseconds;
        }

        public void Engage() => _remaining = _holdMilliseconds;
        public void Tick(int milliseconds) => _remaining = Math.Max(0, _remaining - milliseconds);
    }
}
