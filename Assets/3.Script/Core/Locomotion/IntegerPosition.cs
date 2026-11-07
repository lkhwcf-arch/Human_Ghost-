using System;

namespace HumanGhost.Gameplay
{
    // 한 타일을 백만 단위로 보존합니다. 시트·저장 파일의 1/100 타일 단위는 바꾸지 않습니다.
    public readonly struct IntegerPosition : IEquatable<IntegerPosition>
    {
        public const long UnitsPerTile = 1000000;
        public long X { get; }
        public long Y { get; }
        public IntegerPosition(long x, long y) { X = x; Y = y; }
        public static long FromData(int value) => checked(value * (UnitsPerTile / 100));
        public static int ToData(long value)
        {
            const long unit = UnitsPerTile / 100;
            long quotient = value / unit, remainder = value % unit;
            // 기존 저장 좌표의 중간값 짝수 반올림을 정수 연산으로 유지합니다.
            if (Math.Abs(remainder) > unit / 2 || (Math.Abs(remainder) == unit / 2 && quotient % 2 != 0))
                quotient += Math.Sign(remainder);
            return checked((int)quotient);
        }
        public bool Equals(IntegerPosition other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is IntegerPosition other && Equals(other);
        public override int GetHashCode() => X.GetHashCode() ^ Y.GetHashCode();
    }
}
