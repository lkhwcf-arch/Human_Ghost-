using System;
using HumanGhost.Gameplay;

internal static class IntegerMotionChecks
{
    public static void Run(Action<bool, string> check)
    {
        var first = new IntegerMotion();
        var second = new IntegerMotion();
        long divided = 0;
        for (int i = 0; i < 1000; i++) divided += first.StepHorizontal(1, 1);
        check(divided == IntegerPosition.FromData(1) && divided == second.StepHorizontal(1, 1000), "낮은 속도와 프레임 분할에서도 이동 잔여량 보존");
        check(first.StepHorizontal(-100, 1000) == -IntegerPosition.UnitsPerTile, "음수 방향 이동");
        first.StartRoll(-1, 317, 333);
        long rolled = first.StepRoll(10) + first.StepRoll(70) + first.StepRoll(400);
        check(rolled == -IntegerPosition.FromData(317) && !first.IsRolling, "구르기 종료 시 데이터 거리와 정확히 일치");
        first.StartRoll(1, 100, 100); first.StopRoll();
        check(first.StepRoll(100) == 0, "취소한 구르기는 추가 이동하지 않음");
        first.ConfigureJump(IntegerPosition.FromData(300), 800);
        second.ConfigureJump(IntegerPosition.FromData(300), 800);
        first.StartJump(); second.StartJump();
        long peak = first.StepVertical(400);
        check(peak == IntegerPosition.FromData(300), "기존 점프의 목표 높이와 최고점 도달 시간 유지");
        long end = peak + first.StepVertical(400);
        check(end == 0, "800ms 점프의 시작 높이 복귀");
        long partitioned = 0;
        for (int i = 0; i < 80; i++) partitioned += second.StepVertical(10);
        check(partitioned == end, "정수 점프 적분의 프레임 분할 일치");
        first.SetVerticalSpeed(); second.SetVerticalSpeed();
        long fall = first.StepVertical(3000), splitFall = 0;
        for (int i = 0; i < 300; i++) splitFall += second.StepVertical(10);
        check(fall == splitFall, "최대 낙하 속도에 도달해도 프레임 분할 일치");
        first.SetVerticalSpeed();
        check(first.StepVertical(0) == 0, "시간이 흐르지 않으면 이동하지 않음");
        check(IntegerPosition.ToData(15000) == 2 && IntegerPosition.ToData(25000) == 2
            && IntegerPosition.ToData(-15000) == -2 && IntegerPosition.ToData(-25000) == -2, "저장 좌표의 중간값 짝수 반올림 유지");
        check(IntegerPosition.ToData(IntegerPosition.FromData(-12345)) == -12345, "음수 저장 좌표 왕복 보존");
        bool rejected = false;
        try { first.StepHorizontal(1, -1); } catch (ArgumentOutOfRangeException) { rejected = true; }
        check(rejected, "음수 시간 거부");
    }
}
