using System;
using HumanGhost.Gameplay;
using HumanGhost.Gameplay.Adapters;

internal static class SoundPlaybackChecks
{
    public static void Run(Action<bool, string> check)
    {
        var scope = new SoundRequestScope();
        scope.Open();
        var oldMusic = scope.Begin(1, true);
        var music = scope.Begin(2, true);
        check(!scope.IsCurrent(oldMusic) && scope.IsCurrent(music), "늦은 이전 배경음 완료 무시");
        var first = scope.Begin(3, false);
        var second = scope.Begin(3, false);
        check(scope.IsCurrent(first) && scope.IsCurrent(second), "동일 효과음 중첩 재생 요청 보존");
        scope.Stop(3);
        check(!scope.IsCurrent(first) && !scope.IsCurrent(second) && scope.IsCurrent(music), "효과음 정지가 다른 배경음 요청에 영향을 주지 않음");
        scope.Close(); scope.Open();
        check(!scope.IsCurrent(music), "씬 종료·재활성화 뒤 이전 음원 완료 무시");
        check(scope.IsCurrent(scope.Begin(3, false)), "새 수명에서 같은 사운드 재생 허용");
        var envelope = new SoundEnvelope(1000, 500);
        envelope.Tick(500);
        check(envelope.Gain == 5000, "1000ms 페이드인 중간 음량");
        envelope.Stop(); envelope.Tick(250);
        check(envelope.Gain == 2500, "페이드인 중 정지해도 현재 크기에서 페이드아웃");
        envelope.Stop(); envelope.Tick(250);
        check(envelope.IsStopped, "정지 중 반복 정지가 종료 시간을 늘리지 않음");
        var instant = new SoundEnvelope(0, 0);
        check(instant.Gain == 10000, "페이드인0은 즉시 재생");
        instant.Stop();
        check(instant.IsStopped, "페이드아웃0은 즉시 종료");
        var longFade = new SoundEnvelope(int.MaxValue, 0);
        longFade.Tick(int.MaxValue);
        check(longFade.Gain == 10000, "긴 정수 시간에서도 음량 계산 범위 보존");
        int released = 0;
        var lease = new AssetLease<string>("음원", () => released++);
        lease.Dispose(); lease.Dispose();
        check(released == 1, "리소스 핸들 한 번만 반환");
    }
}
