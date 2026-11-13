using System;
using System.Collections.Generic;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay;
using HumanGhost.Gameplay.Effects;

internal static class FieldEffectChecks
{
    public static void Run(Action<bool, string> check)
    {
        var fire = new EffectDefinition(1, EffectExecution.Periodic, EffectOperation.Damage, 3, 100, category: EffectCategory.Fire);
        var ice = new EffectDefinition(2, EffectExecution.Maintained, EffectOperation.KnockbackRate, 20000, category: EffectCategory.Ice);
        int damage = 0;
        var target = new EffectTarget(10, 1, () => true, value => { damage += value; return true; });
        var session = new EffectSession();
        var events = new List<EffectResult>(); session.Changed += events.Add;
        session.Enter(20, new[] { fire, ice }, target);
        check(damage == 3 && session.NeedsTick, "접촉 즉시 피해와 복합 효과 실행");
        session.Enter(20, new[] { fire }, target);
        session.Tick(99); check(damage == 3, "중복 접촉과 주기 이전 피해 차단");
        session.Tick(201); check(damage == 12, "누적 시간의 정수 주기 처리");
        int capturedDistance = target.ResolveKnockbackDistance(200);
        session.Exit(20, target);
        session.Tick(1000);
        check(damage == 12 && !session.NeedsTick && capturedDistance == 400 && target.ResolveKnockbackDistance(200) == 200,
            "이탈 즉시 피해 중단·보정 해제·확정한 이동 거리 유지");
        session.Enter(20, new[] { fire }, target); check(damage == 15, "재진입은 새 실행 상태로 즉시 적용");
        target.Dispose(); session.Tick(500);
        check(damage == 15 && !session.NeedsTick && events.Exists(result => result.Phase == EffectPhase.Ended), "대상 종료 후 예약·표현 종료 기록 정리");
        var reused = new EffectTarget(10, 2, () => true, value => { damage += value; return true; });
        session.Enter(20, new[] { fire }, reused);
        check(damage == 18 && events.Exists(result => result.Generation == 2), "같은 ID의 새 수명에 이전 접촉 기록을 적용하지 않음");
        session.Dispose(); session.Dispose(); reused.Dispose();

        int immuneDamage = 0;
        var immune = new EffectTarget(11, 1, () => true, value => { immuneDamage += value; return true; }, new[] { EffectCategory.Fire });
        using (var immuneSession = new EffectSession())
        {
            immuneSession.Enter(1, new[] { fire, ice }, immune); immuneSession.Tick(1000);
            check(immuneDamage == 0 && immune.ResolveKnockbackDistance(200) == 400, "몬스터 공통 분류 면역은 다른 효과를 막지 않음");
            immuneSession.Enter(2, new[] { ice }, immune);
            check(immune.ResolveKnockbackDistance(200) == 400, "겹친 얼음은 가장 높은 배율 하나 사용");
            immuneSession.Exit(1, immune);
            check(immune.ResolveKnockbackDistance(200) == 400, "다른 영역의 효과 등록은 유지");
        }
        check(immune.ResolveKnockbackDistance(200) == 200, "세션 종료 후 보정 원복");

        int secondTicks = 0;
        EffectTarget dying = null;
        dying = new EffectTarget(1, 1, () => true, value => { dying.Dispose(); return true; });
        var second = new EffectTarget(2, 1, () => true, value => { secondTicks++; return true; });
        var delayed = new EffectDefinition(3, EffectExecution.Periodic, EffectOperation.Damage, 1, 10, false);
        using (var deathSession = new EffectSession())
        {
            var deathEvents = new List<EffectPhase>();
            deathSession.Changed += result => { if (result.TargetID == 1) deathEvents.Add(result.Phase); };
            deathSession.Enter(1, new[] { delayed }, dying); deathSession.Enter(1, new[] { delayed }, second);
            deathSession.Tick(10);
            check(secondTicks == 1, "시간 진행 도중 첫 대상이 종료돼도 다음 대상 처리 유지");
            check(deathEvents.IndexOf(EffectPhase.Applied) < deathEvents.IndexOf(EffectPhase.Ended), "치명 피해 도중 종료돼도 적용 결과 다음 종료 결과 전달");
        }

        bool failed = false;
        using (var broken = new EffectSession())
        {
            broken.Changed += result => { if (result.Phase == EffectPhase.Applied) throw new InvalidOperationException("검사 전용 소비자 실패"); };
            try { broken.Enter(1, new[] { ice }, second); } catch (InvalidOperationException) { failed = true; }
            check(failed && second.ResolveKnockbackDistance(200) == 200, "시작 알림 실패는 호출자에게 전달하고 부분 적용 보정은 정리");
        }
        var definition = TestDefinitions.Player();
        var attacks = new[] { new AttackSpec(10, 10, 10, 0, 0, 0, 0), new AttackSpec(10, 10, 10, 0, 0, 0, 0),
            new AttackSpec(10, 10, 10, 0, 0, 0, 0), new AttackSpec(10, 10, 10, 0, 0, 0, 0), new AttackSpec(10, 10, 10, 0, 0, 0, 0) };
        var player = new PlayerActor(definition, attacks, true, definition.StaminaMax);
        var previousState = player.CurrentState;
        check(player.ReceiveFieldDamage(1) && player.Health == definition.HP - 1 && player.CurrentState == previousState && !player.IsInvulnerable,
            "필드 피해는 체력만 변경하고 피격 경직·새 무적을 부여하지 않음");
        player.SetCutsceneDamageBlocked(true); check(!player.ReceiveFieldDamage(1), "기존 피해 차단 규칙 재사용");
        player.SetCutsceneDamageBlocked(false); player.ReceiveFieldDamage(player.Health);
        check(player.IsDead && player.CurrentState == StateKey.Dead, "필드 피해도 기존 사망 상태 사용");
        var monster = new MonsterActor(TestDefinitions.Monster(false), new[] {
            new MonsterPattern(StateKey.Attack1, 100, new AttackSpec(10, 10, 10, 0, 0, 0, 0)) }, 100, 200, (min, max) => min);
        previousState = monster.CurrentState;
        check(monster.ReceiveFieldDamage(1) && monster.CurrentState == previousState, "몬스터 필드 피해는 행동을 끊지 않음");
        monster.ReceiveFieldDamage(monster.Health); check(monster.IsDead, "몬스터 필드 사망 경로 재사용");
    }
}
