using System;

namespace HumanGhost.Gameplay
{
    // 제곱 거리의 128비트 정수 표현입니다. 매 프레임 대상 선택에 큰 정수 객체를 할당하지 않습니다.
    public readonly struct IntegerSquaredDistance : IComparable<IntegerSquaredDistance>
    {
        private readonly ulong _high, _low;
        internal IntegerSquaredDistance(ulong high, ulong low) { _high = high; _low = low; }
        public int CompareTo(IntegerSquaredDistance other) => _high != other._high ? _high.CompareTo(other._high) : _low.CompareTo(other._low);
        internal static IntegerSquaredDistance Square(ulong value)
        {
            const ulong mask = uint.MaxValue;
            ulong low = value & mask, high = value >> 32;
            ulong bottom = low * low, cross = high * low;
            ulong middle = (bottom >> 32) + (cross & mask) * 2;
            return new IntegerSquaredDistance(high * high + (cross >> 32) * 2 + (middle >> 32),
                (middle << 32) | (bottom & mask));
        }
        internal static IntegerSquaredDistance Add(IntegerSquaredDistance left, IntegerSquaredDistance right)
        {
            ulong low = unchecked(left._low + right._low);
            return new IntegerSquaredDistance(checked(left._high + right._high + (low < left._low ? 1UL : 0UL)), low);
        }
    }

    public static class IntegerDistance
    {
        // 반경 밖 축을 먼저 제외하므로 두 제곱의 합도 128비트 안에 들어옵니다.
        public static bool TryWithinRadius(IntegerPosition point, IntegerPosition center, long radius, out IntegerSquaredDistance distance)
        {
            if (radius < 0) throw new ArgumentOutOfRangeException(nameof(radius));
            ulong x = Difference(point.X, center.X), y = Difference(point.Y, center.Y);
            distance = default;
            if (x > (ulong)radius || y > (ulong)radius) return false;
            distance = IntegerSquaredDistance.Add(IntegerSquaredDistance.Square(x), IntegerSquaredDistance.Square(y));
            return distance.CompareTo(IntegerSquaredDistance.Square((ulong)radius)) <= 0;
        }
        // 0 이상 정수의 내림 제곱근입니다. 이동 경로 길이 등 거리 계산을 실수 없이 할 때 씁니다.
        public static long Sqrt(long value)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            if (value < 2) return value;
            long low = 1, high = Math.Min(value, 3037000499L); // 3037000499² ≤ long.MaxValue
            while (low < high)
            {
                long middle = low + (high - low + 1) / 2;
                if (middle <= value / middle) low = middle; else high = middle - 1;
            }
            return low;
        }
        private static ulong Difference(long left, long right) => left >= right
            ? unchecked((ulong)left - (ulong)right) : unchecked((ulong)right - (ulong)left);
    }
}
