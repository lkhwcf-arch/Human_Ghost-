using System;
using System.IO;
using HumanGhost.Gameplay;
using HumanGhost.Gameplay.Adapters;

internal static class DemoEndingChecks
{
    public static void Run(Action<bool, string> check)
    {
        foreach (var reason in new[] { CutsceneEndReason.Completed, CutsceneEndReason.Skipped })
        {
            var flow = new DemoEndingFlow(false);
            check(!flow.TryBegin(false) && !flow.TryFinish(), "보스 처치 전 영상·마무리 차단");
            check(flow.TryBegin(true) && !flow.TryBegin(true), "엔딩 영상 시작 중복 차단");
            check(!flow.TryFinish(), "영상 중 다음 이동 차단");
            flow.EndMovie(reason);
            check(flow.CanFinish && flow.Phase == DemoEndingPhase.Exploring, "영상 종료·스킵 후 맵 탐색 유지");
            check(!flow.Complete(), "이동 시도 없이 타이틀 진행 차단");
            check(flow.TryFinish() && !flow.TryFinish(), "이동 시도 후 마무리 한 번만 시작");
            check(flow.Complete() && !flow.Complete(), "마무리 확인 뒤 한 번만 타이틀 진행");
        }
        foreach (var reason in new[] { CutsceneEndReason.Cancelled, CutsceneEndReason.Failed })
        {
            var flow = new DemoEndingFlow(false);
            flow.TryBegin(true); flow.EndMovie(reason);
            check(!flow.CanFinish && !flow.TryFinish(), "영상 실패·취소를 완료로 처리하지 않음");
        }
        var restored = new DemoEndingFlow(true);
        check(restored.CanFinish && !restored.TryBegin(true), "시청 이력 복원 시 영상 반복 없이 출구 허용");
        restored.Fail(); check(!restored.TryFinish(), "실패 뒤 자동 마무리 차단");
        string root = Path.Combine(Path.GetTempPath(), "HumanGhostVideoChecks");
        check(StreamingVideoAddress.TryResolve("streaming:Video/Ending.mp4", root, out string file)
            && file == Path.GetFullPath(Path.Combine(root, "Video/Ending.mp4")), "영상 상대 경로 해석");
        check(!StreamingVideoAddress.TryResolve("Cutscene_Intro_Video", root, out _), "기존 주소 로딩 계약 유지");
        foreach (string invalid in new[] { "streaming:../Ending.mp4", "streaming:/Ending.mp4", "streaming:https://example.com/a.mp4", "streaming:Video/a.txt" })
        {
            bool rejected = false;
            try { StreamingVideoAddress.TryResolve(invalid, root, out _); }
            catch (ArgumentException) { rejected = true; }
            check(rejected, "영상 경로 이탈·외부 주소·다른 확장자 거부");
        }
    }
}
