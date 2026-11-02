using System;
using System.Collections.Generic;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay;

internal static class ModuleChecks
{
    public static int Run(Player definition, Func<AttackSpec[]> specs)
    {
        int checks = 0;
        void Check(bool result, string message) { if (!result) throw new Exception(message); checks++; }
        void ExpectInvalid(Action action)
        {
            try { action(); } catch (ArgumentOutOfRangeException) { checks++; return; }
            throw new Exception("잘못된 프레임 시간 거부 누락");
        }

        // 공통 시계로 바꿔도 개체별 나머지와 프레임 분할 결과가 유지돼야 합니다.
        var clock = new MillisecondClock();
        var other = new MillisecondClock();
        Check(clock.Advance(0.00025) == 0 && clock.Advance(0.00025) == 0 && clock.Advance(0.00025) == 0, "소수 ms 누적");
        Check(other.Advance(0) == 0 && clock.Advance(0.00025) == 1, "개체별 시계 나머지 독립");
        var divided = new MillisecondClock();
        int total = 0;
        for (int i = 0; i < 128; i++) total += divided.Advance(1.0 / 128);
        Check(total == 1000 && new MillisecondClock().Advance(1) == total, "프레임 분할 시간 보존");
        var large = new MillisecondClock();
        Check(large.Advance(3000000) == int.MaxValue && large.Advance(0) == 3000000000L - int.MaxValue, "큰 프레임의 int 상한과 남은 시간 보존");
        clock.Advance(0.0005);
        ExpectInvalid(() => clock.Advance(-1));
        ExpectInvalid(() => clock.Advance(double.NaN));
        ExpectInvalid(() => clock.Advance(double.PositiveInfinity));
        ExpectInvalid(() => clock.Advance(double.MaxValue));
        Check(clock.Advance(0.0005) == 1, "거부한 시간 입력이 기존 나머지를 바꾸지 않음");

        // Actor 공개 API로 설정 스냅샷·판정 창·큐·이벤트의 독립성을 확인합니다.
        AttackSpec[] source = specs();
        AttackSpec first = source[0];
        var a = new PlayerActor(definition, source, true, definition.StaminaMax);
        var b = new PlayerActor(definition, source, true, definition.StaminaMax);
        source[0] = new AttackSpec(1, 1, 0, 0, 999, 0, 0);
        a.TryAttack(); b.TryAttack();
        Check(ReferenceEquals(a.CurrentAttack, first) && ReferenceEquals(b.CurrentAttack, first), "호출자가 배열을 바꿔도 공격 스냅샷 유지");
        var transitions = new List<bool>();
        Action<bool> handler = transitions.Add;
        a.AttackWindowChanged += handler;
        a.Tick(first.Windup); b.Tick(first.Windup);
        Check(a.TryRegisterTarget(7) && b.TryRegisterTarget(7) && !a.TryRegisterTarget(7), "같은 대상 ID도 개체별 창에서 한 번씩 허용");
        a.Tick(first.Active); b.Tick(first.Active);
        a.TryAttack();
        a.Tick(first.Recovery); b.Tick(first.Recovery);
        Check(a.CurrentState == StateKey.Attack2 && !b.IsAttacking, "한 개체의 콤보 큐가 다른 개체로 전파되지 않음");
        Check(transitions.Count == 2 && transitions[0] && !transitions[1], "공유 모듈 이벤트가 기존 Actor에 한 번씩 전달");
        a.AttackWindowChanged -= handler;
        a.Tick(first.Windup);
        Check(a.TryRegisterTarget(7) && transitions.Count == 2, "다음 타에서 대상 등록 초기화·이벤트 해제 유지");
        a.ReceiveHit(1);
        Check(!a.IsAttacking && !a.IsAttackActive && !a.TryRegisterTarget(8), "피격 중단이 콤보와 판정 창을 함께 정리");
        return checks;
    }
}
