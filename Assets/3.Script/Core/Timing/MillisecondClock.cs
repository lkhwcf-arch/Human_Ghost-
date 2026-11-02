using System;

namespace HumanGhost.Gameplay
{
    // 프레임 초를 정수 ms로 바꾸고 나머지는 런타임별로 보존합니다.
    internal struct MillisecondClock
    {
        private double _remainder;

        public int Advance(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0 || seconds > double.MaxValue / 1000.0)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            double total = _remainder + seconds * 1000.0;
            if (double.IsInfinity(total)) throw new ArgumentOutOfRangeException(nameof(seconds));
            int milliseconds = (int)Math.Min(int.MaxValue, Math.Floor(total));
            _remainder = total - milliseconds;
            return milliseconds;
        }
    }
}
