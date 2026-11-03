using System;
using HumanGhost.Gameplay;

internal static class CutsceneChecks
{
    public static void Run(Action<bool, string> check)
    {
        var steps = new[] {
            new CutsceneStep(CutsceneMedia.Image, "Ledger01", 4000),
            new CutsceneStep(CutsceneMedia.Image, "EndingCard", 3000, 30025, false)
        };
        var sequence = new CutsceneSequence("Ledger", steps, 2000);
        steps[0] = null;
        check(sequence.Steps[0] != null, "재생 정의는 호출자의 목록 변경과 독립");
        var playback = new CutscenePlayback();
        void Start(CutsceneSequence next, bool ready = false)
        {
            if (!playback.TryPlay(next) || (ready && !playback.MarkReady(playback.StepToken)))
                throw new InvalidOperationException("컷씬 검사 재생 준비에 실패했습니다.");
        }
        int finished = 0;
        CutsceneEndReason? result = null;
        playback.Finished += reason => { finished++; result = reason; };
        check(playback.TryPlay(sequence) && !playback.TryPlay(sequence), "재생 중 중복 시작 거부");
        long first = playback.StepToken;
        playback.Tick(10000);
        check(playback.StepIndex == 0 && playback.State == CutscenePlaybackState.Preparing, "리소스 준비 대기는 표시 시간을 소비하지 않음");
        check(!playback.MarkReady(first - 1) && playback.MarkReady(first), "현재 구간의 준비 결과만 반영");
        playback.Tick(3999);
        check(playback.StepIndex == 0, "그림 표시 시간 직전 유지");
        playback.Tick(1);
        check(playback.StepIndex == 1 && playback.State == CutscenePlaybackState.Preparing, "정확한 표시 시간에 다음 구간 준비");
        check(!playback.ReportVideoEnded(first), "그림과 이전 구간의 영상 완료 신호 무시");
        playback.MarkReady(playback.StepToken);
        check(!playback.Advance(), "넘기기 금지 구간 유지");
        playback.Tick(int.MaxValue);
        check(result == CutsceneEndReason.Completed && finished == 1, "큰 프레임 시간에도 안전하게 자연 종료");
        check(!playback.Cancel() && !playback.Fail(playback.StepToken, "지연 실패"), "종료 결과는 한 번만 전달");

        Start(sequence);
        check(playback.StepToken != first && !playback.MarkReady(first), "재시작 후 이전 준비 콜백 무시");
        playback.MarkReady(playback.StepToken);
        playback.SetSkipHeld(true);
        playback.Tick(1999);
        check(playback.IsRunning && playback.SkipElapsedMs == 1999, "2000밀리초 직전에는 건너뛰지 않음");
        playback.SetSkipHeld(false);
        check(playback.SkipElapsedMs == 0, "키를 떼면 건너뛰기 누적 초기화");
        playback.SetSkipHeld(true);
        playback.Tick(2000);
        check(result == CutsceneEndReason.Skipped && finished == 2, "유지 시간 충족 시 건너뛰기 종료");

        Start(sequence, ready: true);
        check(playback.Advance() && playback.StepIndex == 1, "그림 다음 장 요청");
        check(playback.Cancel() && result == CutsceneEndReason.Cancelled, "준비 중에도 명시적 취소");

        var video = new CutsceneSequence("Intro", new[] { new CutsceneStep(CutsceneMedia.Video, "IntroVideo", 0) }, 2000);
        Start(video);
        long token = playback.StepToken;
        check(!playback.ReportVideoEnded(token), "준비 중 영상 종료 신호는 무시");
        playback.MarkReady(token);
        playback.Tick(int.MaxValue);
        check(playback.IsRunning && !playback.Advance(), "영상은 타이머나 다음 장 입력으로 종료하지 않음");
        check(playback.ReportVideoEnded(token) && result == CutsceneEndReason.Completed, "현재 영상 종료 신호 반영");
        Start(video);
        check(playback.Fail(playback.StepToken, "영상 준비 실패") && playback.FailureMessage == "영상 준비 실패"
            && result == CutsceneEndReason.Failed, "준비 실패와 자연 종료 구분");
    }
}
