using System;
using HumanGhost.Gameplay;

// 비탈길 설계(2026-11-17)의 정수 경사 규칙을 Unity 없이 확인합니다.
internal static class GroundSlopeRuleChecks
{
    public static void Run(Action<bool, string> check)
    {
        long tile = IntegerPosition.UnitsPerTile;
        check(GroundSlopeRule.CanTraverse(tile, tile) && GroundSlopeRule.CanTraverse(tile, -tile), "45° 오르막·내리막 허용");
        check(GroundSlopeRule.CanTraverse(2 * tile, tile), "26.6°(기울기 0.5) 허용");
        check(!GroundSlopeRule.CanTraverse(tile, tile + tile / 100), "45° 초과(기울기 1.01) 거부");
        check(!GroundSlopeRule.CanTraverse(0, tile / 100), "제자리 상승 거부");
        check(GroundSlopeRule.CanTraverse(tile, tile + GroundSlopeRule.ObservationTolerance), "물리 관측 오차 범위는 허용");
        // 좌표가 큰 곳의 float 관측 오차는 어댑터가 넘긴 만큼만 더 허용합니다(45° 정확한 면이 오차로 거부되지 않음).
        long error = tile / 1000;
        check(!GroundSlopeRule.CanTraverse(tile, tile + GroundSlopeRule.ObservationTolerance + error), "관측 오차를 넘기지 않으면 엄격 판정");
        check(GroundSlopeRule.CanTraverse(tile, tile + GroundSlopeRule.ObservationTolerance + error, error), "넘긴 관측 오차만큼 허용");
        check(GroundSlopeRule.MaxRise(tile, error) == GroundSlopeRule.MaxRise(tile) + error, "관측 오차는 최대 높이에 그대로 더함");
        bool negative = false;
        try { GroundSlopeRule.CanTraverse(tile, tile, -1); } catch (ArgumentOutOfRangeException) { negative = true; }
        check(negative, "음수 관측 오차 거부");

        // 같은 45.5° 면을 30·60·144fps 한 걸음(초당 3칸)으로 나눠도 매 걸음 거부해야 프레임률과 무관합니다.
        foreach (int fps in new[] { 30, 60, 144 })
        {
            long run = 3 * tile / fps;
            long rise = run * 10175 / 10000;
            check(!GroundSlopeRule.CanTraverse(run, rise), fps + "fps 한 걸음의 45.5° 거부");
            check(GroundSlopeRule.CanTraverse(run, run), fps + "fps 한 걸음의 45° 허용");
        }

        check(GroundSlopeRule.IsWalkableNormal(7071, 7071) && GroundSlopeRule.IsWalkableNormal(-7071, 7071), "45° 법선 걷기 가능");
        check(GroundSlopeRule.IsWalkableNormal(-4472, 8944), "26.6° 법선 걷기 가능");
        check(!GroundSlopeRule.IsWalkableNormal(7660, 6428), "50° 법선은 벽");
        check(!GroundSlopeRule.IsWalkableNormal(10000, 0) && !GroundSlopeRule.IsWalkableNormal(0, -10000), "수직 벽·천장은 걷기 불가");
        bool rejected = false;
        try { GroundSlopeRule.MaxRise(long.MinValue); } catch (ArgumentOutOfRangeException) { rejected = true; }
        check(rejected, "표현할 수 없는 이동량 거부");

        // 비탈 경로 길이를 실수 없이 구하는 정수 내림 제곱근입니다.
        check(IntegerDistance.Sqrt(0) == 0 && IntegerDistance.Sqrt(1) == 1 && IntegerDistance.Sqrt(15) == 3 && IntegerDistance.Sqrt(16) == 4,
            "정수 제곱근 경계");
        check(IntegerDistance.Sqrt(2 * tile * tile) == 1414213, "45° 한 칸 경로 길이(√2칸)");
        check(IntegerDistance.Sqrt(long.MaxValue) == 3037000499L, "정수 제곱근 최대값");
    }
}
