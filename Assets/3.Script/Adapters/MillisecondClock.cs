using System;

namespace HumanGhost.Gameplay
{
    // 프레임 초를 정수 ms로 바꾸고 나머지는 런타임별로 보존합니다.
    internal struct MillisecondClock
    {
        private double _remainder;

        // 클립 길이 등 외부 API의 단일 초 값을 정수 ms로 변환합니다. 프레임 누적에는 Advance를 사용합니다.
        public static int ToMilliseconds(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0 || seconds > int.MaxValue / 1000.0)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            return checked((int)Math.Round(seconds * 1000.0, MidpointRounding.AwayFromZero));
        }

        // Unity 단계 실행에서 생기는 유한한 음수 프레임은 건너뛰고 나머지를 보존합니다.
        // 일반 시간 입력의 엄격한 검사는 Advance에서 유지합니다.
        public int AdvanceFrame(double seconds) =>
            seconds < 0 && !double.IsInfinity(seconds) ? 0 : Advance(seconds);

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

        // 역방향 클립 샘플링도 같은 나머지를 보존합니다. 게임 규칙 시간에는 음수를 허용하는 이 경로를 쓰지 않습니다.
        public int AdvanceSigned(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || Math.Abs(seconds) > int.MaxValue / 1000.0)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            double total = _remainder + seconds * 1000.0;
            int milliseconds = checked((int)Math.Truncate(total));
            _remainder = total - milliseconds;
            return milliseconds;
        }
    }
}
