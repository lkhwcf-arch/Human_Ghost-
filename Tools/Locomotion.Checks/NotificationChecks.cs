using System;
using System.Collections.Generic;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay;

internal static class NotificationChecks
{
    public static int Run(Player definition)
    {
        int checks = 0;
        void Check(bool result, string message) { if (!result) throw new Exception(message); checks++; }
        var spec = new AttackSpec(1, 1, 1, 0, 0, 0, 0);
        var actor = new PlayerActor(definition, new[] { spec, spec, spec, spec, spec }, true, definition.StaminaMax);
        var scope = new SubscriptionScope();
        Action invalidate = scope.Invalidate;
        scope.Add(() => actor.DisplayChanged -= invalidate);
        actor.DisplayChanged += invalidate;
        actor.ConfigureHerbs(1, 10, 3);
        Check(scope.TryConsumeRefresh(), "약초 최초 연결 통지");
        actor.RestoreVitals(definition.HP - 1, 2);
        Check(scope.TryConsumeRefresh() && actor.Health == definition.HP - 1 && actor.HerbCount == 2, "복원 완료 값 반영 예약");
        Check(!scope.TryConsumeRefresh(), "한 호출의 여러 변경 통지를 표시 한 번으로 병합");
        Check(actor.TryUseHerb() && scope.TryConsumeRefresh() && actor.HerbCount == 1, "약초 소비와 행동 상태 변경 통지");
        actor.ConfigureSkill(spec, 2500, 0);
        Check(scope.TryConsumeRefresh() && actor.HasSkill, "스킬 연결 통지");
        int changed = 0;
        actor.DisplayChanged += () => changed++;
        scope.Dispose();
        actor.ConfigureHerbs(1, 10, 3);
        scope.Invalidate();
        Check(changed > 0 && !scope.TryConsumeRefresh(), "해제된 표시 수명에는 예약이 남지 않음");

        var seconds = new List<int>();
        var skill = new PlayerSkill(spec, 2500, 0);
        skill.CooldownSecondsChanged += seconds.Add;
        skill.StartCooldown();
        Check(seconds.Count == 1 && seconds[0] == 3, "쿨타임 시작은 올림 초 통지");
        skill.Tick(499);
        Check(seconds.Count == 1 && skill.Remaining == 2001, "같은 표시 초에서는 통지 없음");
        skill.Tick(1);
        Check(seconds.Count == 2 && seconds[1] == 2, "밀리초 경계에서 표시 초 변경 통지");
        skill.Tick(2000);
        Check(seconds.Count == 3 && seconds[2] == 0 && skill.IsReady, "큰 시간 진행도 완료 값 통지");
        skill.Tick(1000);
        Check(seconds.Count == 3, "완료된 쿨타임의 반복 통지 없음");

        var herbs = new PlayerHerbs(1, 10, 3);
        int herbChanges = 0;
        herbs.Changed += () => herbChanges++;
        herbs.Refill(); herbs.Restore(3);
        Check(herbChanges == 0, "동일 약초 개수 반복 통지 없음");
        herbs.TryConsume(); herbs.Refill();
        Check(herbChanges == 2, "약초 소비와 재충전 통지");

        var progress = new StageProgress();
        int progressChanges = 0;
        progress.Changed += () => progressChanges++;
        progress.Clear(8001); progress.Clear(8001);
        Check(progressChanges == 1, "클리어 조건의 실제 변경만 통지");

        var pending = new SubscriptionScope();
        pending.Invalidate(); pending.Invalidate();
        Check(pending.TryConsumeRefresh() && !pending.TryConsumeRefresh(), "여러 요청을 한 번으로 병합");
        pending.Invalidate(); pending.Dispose(); pending.Dispose();
        Check(!pending.TryConsumeRefresh(), "중복 해제 안전·미처리 예약 제거");
        int guardedCalls = 0;
        var lifetime = new SubscriptionScope();
        Action guarded = lifetime.Guard(() => guardedCalls++);
        Action<int> guardedValue = lifetime.Guard<int>(value => guardedCalls += value);
        guarded(); guardedValue(2);
        Check(guardedCalls == 3, "현재 수명의 콜백 실행");
        lifetime.Dispose(); guarded(); guardedValue(2);
        Check(guardedCalls == 3, "해제 전 보관한 콜백도 해제 후 실행하지 않음");
        var order = new List<int>();
        var cleanup = new SubscriptionScope();
        cleanup.Add(() => order.Add(1));
        cleanup.Add(() => { order.Add(2); throw new InvalidOperationException("검사 전용 해제 실패"); });
        cleanup.Add(() => order.Add(3));
        bool reported = false;
        try { cleanup.Dispose(); } catch (AggregateException) { reported = true; }
        Check(reported && order.Count == 3 && order[0] == 3 && order[2] == 1, "한 해제 실패도 나머지 구독 정리 후 보고");
        return checks;
    }
}
