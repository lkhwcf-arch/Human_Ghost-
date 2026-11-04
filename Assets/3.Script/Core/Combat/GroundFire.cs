using System;

namespace HumanGhost.Gameplay
{
    // C08 불붙은 짚: 일정 시간 동안 주기마다 고정 피해 차례를 알립니다. 위치·접촉·피해 적용은 외부가 맡습니다.
    public sealed class GroundFire
    {
        private readonly int _interval;
        private int _remaining;
        private int _untilTick;

        public int Damage { get; }
        public bool IsActive => _remaining > 0;

        public GroundFire(int duration, int damage, int interval)
        {
            if (duration <= 0 || damage <= 0 || interval <= 0) throw new ArgumentOutOfRangeException(nameof(duration));
            _remaining = duration; Damage = damage; _interval = interval; _untilTick = interval;
        }

        // 지난 시간 안에 돌아온 피해 차례 수를 반환합니다.
        public int Tick(int milliseconds)
        {
            if (milliseconds < 0) throw new ArgumentOutOfRangeException(nameof(milliseconds));
            int elapsed = Math.Min(milliseconds, _remaining);
            _remaining -= elapsed;
            int ticks = 0;
            while (elapsed >= _untilTick) { elapsed -= _untilTick; _untilTick = _interval; ticks++; }
            _untilTick -= elapsed;
            return ticks;
        }
    }
}
