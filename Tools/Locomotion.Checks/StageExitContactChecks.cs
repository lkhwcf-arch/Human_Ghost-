using System;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay;

internal static class StageExitContactChecks
{
    public static void Run(Action<bool, string> check)
    {
        // 실제 콘텐츠 수치가 아니라 접촉 계약의 경계 입력입니다.
        StageExit Area(int id, int width) => new StageExit(id, 1, "Area", "Exit", 2, "Arrival", width, 2, 0, 0, true, 0, "Travel");
        long Units(int value) => IntegerPosition.FromData(value);
        var first = new StageExitContact(Area(1, 3));
        check(!first.Observe(Units(0), Units(0), Units(-100), Units(100), Units(0), Units(0)), "도착 지점과 출구가 겹쳐도 즉시 되돌아가지 않음");
        check(!first.Observe(Units(4), Units(0), Units(-100), Units(100), Units(0), Units(0)), "영역 이탈은 이동이 아님");
        check(first.Observe(Units(3), Units(0), Units(-100), Units(100), Units(0), Units(0)), "데이터 반폭 경계에 진입하면 한 번 이동");
        check(!first.Observe(Units(2), Units(0), Units(-100), Units(100), Units(0), Units(0)), "영역 안 반복 관측은 중복 이동하지 않음");
        var right = new StageExitContact(new StageExit(3, 1, "RightEdge", "", 2, "Arrival", 160, 0, 0, 0, true, 0, "Travel"));
        check(!right.Observe(Units(500), 0, 0, Units(1000), 0, 0), "맵 중앙에서 오른쪽 출구 관측 시작");
        check(right.Observe(Units(850), 0, 0, Units(1000), 0, 0), "맵 끝 1.5타일 전은 데이터 160의 감지 범위 안");
        var left = new StageExitContact(new StageExit(4, 2, "LeftEdge", "", 1, "Arrival", 160, 0, 0, 0, true, 0, "Travel"));
        check(!left.Observe(Units(500), 0, 0, Units(1000), 0, 0), "맵 중앙에서 왼쪽 출구 관측 시작");
        check(left.Observe(Units(150), 0, 0, Units(1000), 0, 0), "왼쪽 출구에도 데이터 단위 변환 적용");
        var second = new StageExitContact(Area(2, 1));
        check(!second.Observe(Units(4), Units(0), Units(-100), Units(100), Units(0), Units(0)), "두 번째 출구의 관측 상태는 독립적");
        check(second.Observe(Units(1), Units(0), Units(-100), Units(100), Units(0), Units(0)), "다른 출구가 자기 데이터 범위로 진입을 판정");
        check(!first.Observe(Units(0), Units(3), Units(-100), Units(100), Units(0), Units(0)), "가로 범위 안이어도 세로 범위 밖은 이탈");
        check(first.Observe(Units(0), Units(2), Units(-100), Units(100), Units(0), Units(0)), "영역 이탈 후 재진입 가능");
    }
}
