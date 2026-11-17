using System;

namespace HumanGhost.Gameplay
{
    // 걸을 수 있는 지면 경사의 정수 규칙입니다. Unity와 분리해 코어 검사에서 확인합니다.
    // 경사는 높이/폭 비율을 분모 10000으로 표현하고, 거리는 IntegerPosition 단위(1칸 = 1,000,000)를 씁니다.
    // [인터페이스·인스턴스 미적용 이유] 상태 없는 순수 계산이고 현재 규칙이 하나뿐이라 정적 클래스로 둡니다.
    // 캐릭터별 다른 경사가 데이터로 들어오면 그때 한계값을 받는 인스턴스로 바꿉니다.
    // [데이터 미적용 이유] 최대 경사는 사용자가 11/17 코드 상수 하나로 확정했고, 이를 담을 기존 이동 시트 열이 없습니다.
    public static class GroundSlopeRule
    {
        public const int RatioScale = 10000;
        // 2026-11-17 사용자 확정: 최대 경사 45°(높이/폭 = 1). 캐릭터별 차이가 필요해지면 데이터로 옮깁니다.
        public const int MaxSlopeRatio = 10000;
        // 정수 변환 반올림에 대한 최소 허용입니다(0.00002칸). 좌표 크기에 따라 커지는 float 관측 오차는 이 값에 포함하지 않고
        // 관측한 물리 어댑터가 observationError로 따로 넘깁니다(그 오차는 좌표 위치를 아는 경계만 계산할 수 있습니다).
        public const long ObservationTolerance = IntegerPosition.UnitsPerTile / 50000;

        // 수평 run만큼 이동할 때 허용되는 최대 높이 변화입니다. 이동량에 비례하므로 프레임 분할과 관계없이 같은 경사 한계를 씁니다.
        public static long MaxRise(long run) => MaxRise(run, 0);

        // observationError: 관측 경계가 알려 준 추가 오차(정수, 0 이상)입니다.
        public static long MaxRise(long run, long observationError)
        {
            if (run == long.MinValue) throw new ArgumentOutOfRangeException(nameof(run));
            if (observationError < 0) throw new ArgumentOutOfRangeException(nameof(observationError));
            return checked(Math.Abs(run) * MaxSlopeRatio / RatioScale + ObservationTolerance + observationError);
        }

        public static bool CanTraverse(long run, long rise) => CanTraverse(run, rise, 0);

        public static bool CanTraverse(long run, long rise, long observationError)
        {
            if (rise == long.MinValue) throw new ArgumentOutOfRangeException(nameof(rise));
            return Math.Abs(rise) <= MaxRise(run, observationError);
        }

        // 분모 10000 정수로 받은 면 법선(x, y)이 걸을 수 있는 경사인지 봅니다. |x|/y <= 최대 경사.
        public static bool IsWalkableNormal(int normalX, int normalY)
        {
            if (normalX == int.MinValue) throw new ArgumentOutOfRangeException(nameof(normalX));
            return normalY > 0 && (long)Math.Abs(normalX) * RatioScale <= (long)normalY * MaxSlopeRatio;
        }
    }
}
