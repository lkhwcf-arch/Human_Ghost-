using System;
using HumanGhost.Gameplay;

internal static class AttackAnimationChecks
{
    public static void Run(Action<bool, string> check)
    {
        var attack = new AttackSpec(400,100,400,0,0,0,0);
        double Frame(int elapsed) => AttackAnimationTiming.FrameAt(attack,elapsed,0,7,8,13);
        check(Frame(0) == 0 && Frame(399) < 7, "선딜 동안 타격 프레임 진입 금지");
        check(Frame(400) == 7 && Frame(499) < 8, "판정 시작·종료와 타격 프레임 일치");
        check(Frame(500) == 8 && Frame(900) == 12 && Frame(int.MaxValue) == 12, "후딜·완료는 구간 마지막에서 정지");
        var first = new AttackSpec(500,100,0,0,0,0,0);
        var second = new AttackSpec(250,100,600,0,0,0,0);
        check(AttackAnimationTiming.FrameAt(first,500,0,8,10,13) == 8, "보스 첫 타격 프레임8");
        check(AttackAnimationTiming.FrameAt(first,600,0,8,10,13) == 12, "후딜0 구간은 다음 공격으로 넘길 준비");
        check(AttackAnimationTiming.FrameAt(second,0,13,21,24,26) == 13 &&
            AttackAnimationTiming.FrameAt(second,250,13,21,24,26) == 21, "보스 2타 독립 선딜·프레임21");
        check(AttackAnimationTiming.FrameAt(second,350,13,21,24,26) == 24 &&
            AttackAnimationTiming.FrameAt(second,950,13,21,24,26) == 25, "보스 2타 후딜·마지막 프레임");
        bool phases = true;
        for (int elapsed=0;elapsed<attack.TotalTime;elapsed++)
        {
            double frame=Frame(elapsed);
            if ((frame>=7 && frame<8) != (elapsed>=400 && elapsed<500)) phases=false;
        }
        check(phases,"모든 ms에서 표시 타격 구간과 판정 구간 동일");
        bool negative=false,order=false,missing=false;
        try { Frame(-1); } catch (ArgumentOutOfRangeException) {negative=true;}
        try { AttackAnimationTiming.FrameAt(attack,0,0,8,7,13); } catch (ArgumentOutOfRangeException) {order=true;}
        try { AttackAnimationTiming.FrameAt(null,0,0,7,8,13); } catch (ArgumentNullException) {missing=true;}
        check(negative && order && missing,"잘못된 시간·프레임·정의 거부");
    }
}
