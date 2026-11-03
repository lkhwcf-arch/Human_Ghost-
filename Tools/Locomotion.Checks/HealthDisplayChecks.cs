using System;
using System.Collections.Generic;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay;
using HumanGhost.Gameplay.Presentation;

internal static class HealthDisplayChecks
{
    public static int Run(Player definition)
    {
        int checks = 0;
        void Check(bool result, string message) { if (!result) throw new Exception(message); checks++; }
        void Expect<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T) { checks++; return; }
            throw new Exception("예상 예외 누락: " + typeof(T).Name);
        }
        PlayerActor NewPlayer() => new PlayerActor(definition, new[]
        {
            new AttackSpec(1,1,1,0,0,0,0), new AttackSpec(1,1,1,0,0,0,0),
            new AttackSpec(1,1,1,0,0,0,0), new AttackSpec(1,1,1,0,0,0,0),
            new AttackSpec(1,1,1,0,0,0,0)
        }, true, definition.StaminaMax);

        var shown = new List<(int current, int maximum)>();
        var binding = new HealthDisplayBinding((current, maximum) => shown.Add((current, maximum)));
        binding.SetActive(true);
        Check(shown.Count == 0 && !binding.IsBound, "대상 준비 전 표시하지 않음");
        var first = NewPlayer();
        first.ReceiveHit(5);
        binding.Bind(first);
        Check(shown.Count == 1 && shown[0] == (first.Health, definition.HP), "초기 체력이 아닌 현재 체력으로 최초 동기화");
        binding.Bind(first);
        int before = shown.Count;
        first.Tick(definition.HitIFrame + definition.HitStunTime);
        first.ReceiveHit(1);
        Check(shown.Count == before + 1 && shown[shown.Count - 1].current == first.Health, "동일 대상 재연결 시 구독 한 벌");

        binding.SetActive(false);
        before = shown.Count;
        var second = NewPlayer();
        binding.Bind(second);
        second.ReceiveHit(2);
        Check(shown.Count == before && binding.IsBound, "비활성 대상 교체·체력 변경은 표시하지 않음");
        binding.SetActive(true);
        Check(shown.Count == before + 1 && shown[shown.Count - 1].current == second.Health, "재활성화 시 최신 상태 한 번 동기화");
        binding.SetActive(true);
        Check(shown.Count == before + 1, "활성 중복 요청은 구독·표시를 늘리지 않음");
        first.Tick(definition.HitIFrame + definition.HitStunTime);
        first.ReceiveHit(1);
        Check(shown.Count == before + 1, "이전 대상에서 분리됨");

        binding.Unbind();
        binding.Unbind();
        before = shown.Count;
        second.Tick(definition.HitIFrame + definition.HitStunTime);
        second.ReceiveHit(1);
        Check(!binding.IsBound && shown.Count == before, "반환·중복 해제 후 호출 없음");
        Expect<ArgumentNullException>(() => binding.Bind(null));
        Check(!binding.IsBound, "잘못된 대상은 기존 해제 상태 유지");

        var replacement = NewPlayer();
        var old = NewPlayer();
        old.HealthChanged += value => binding.Bind(replacement);
        binding.Bind(old);
        before = shown.Count;
        old.ReceiveHit(3);
        Check(shown.Count == before + 1, "발행 중 대상 교체 후 이전 호출 목록은 적용하지 않음");
        Check(shown[shown.Count - 1].current == replacement.Health, "새 대상 상태만 표시");

        var reactivated = NewPlayer();
        reactivated.HealthChanged += value => { binding.SetActive(false); binding.SetActive(true); };
        binding.Bind(reactivated);
        before = shown.Count;
        reactivated.ReceiveHit(4);
        Check(shown.Count == before + 1, "발행 중 비활성·재활성 시 이전 활성 주기 콜백 거부");

        binding.Dispose();
        binding.Dispose();
        Check(!binding.IsBound, "종료는 대상 참조 제거·중복 종료 허용");
        Expect<ObjectDisposedException>(() => binding.Bind(NewPlayer()));
        Expect<ObjectDisposedException>(() => binding.SetActive(true));
        Expect<ObjectDisposedException>(() => binding.Unbind());
        Expect<ArgumentNullException>(() => new HealthDisplayBinding(null));

        int attempts = 0;
        using var failed = new HealthDisplayBinding((current, maximum) => { attempts++; throw new InvalidOperationException("최초 표시 실패 검사"); });
        failed.SetActive(true);
        var failedSource = NewPlayer();
        Expect<InvalidOperationException>(() => failed.Bind(failedSource));
        failedSource.ReceiveHit(1);
        Check(attempts == 1, "최초 표시 실패 시 구독 정리");
        return checks;
    }
}
