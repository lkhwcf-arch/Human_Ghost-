using System;
using HumanGhost.Gameplay;

internal static class CutsceneChecks
{
    public static void Run(Action<bool, string> check)
    {
        var guard = new VideoEndGuard();
        check(!guard.Step(true, false, false, 359, 361, 14.96, 15.0416667, 24, 10000), "영상 중간 정체는 종료하지 않음");
        check(!guard.Step(true, false, false, 360, 361, 15, 15.0416667, 24, 149), "최종 프레임 짧은 표시만으로 종료하지 않음");
        check(guard.Step(true, false, false, 360, 361, 15, 15.0416667, 24, 1), "최종 프레임 완료 이벤트 누락 복구");
        guard.Reset();
        check(!guard.Step(true, true, false, 360, 361, 15, 15.0416667, 24, 10000), "일시정지 중 마지막 프레임 유지");
        check(!guard.Step(true, false, true, 360, 361, 15, 15.0416667, 24, 10000), "반복 영상 유지");
        check(!guard.Step(false, false, false, 360, 361, 15, 15.0416667, 24, 10000), "준비 전 종료 거부");
        check(!guard.Step(true, false, false, 0, 0, 0, 0, 0, 10000), "미상 길이 영상 종료 거부");
        check(!guard.Step(true, false, false, 360, 361, 14, 15.0416667, 24, 10000), "마지막 번호만으로 조기 종료하지 않음");
        check(!guard.Step(true, false, false, 360, 361, 15, 15.0416667, 24, 100), "새 재생 대기 시작");
        guard.Reset();
        check(!guard.Step(true, false, false, 360, 361, 15, 15.0416667, 24, 100), "새 영상은 이전 끝 대기를 이어받지 않음");
        var dialogue = new TextboxPlayback(new[] { "첫 문장", "다음 문장" }, 30);
        dialogue.Tick(30);
        check(dialogue.SkipLine() && dialogue.CurrentLine == "다음 문장" && dialogue.VisibleCharacters == 0,
            "대사 스킵은 출력 중인 항목을 끝내고 다음 항목 로드");
        check(dialogue.SkipLine() && dialogue.IsFinished && !dialogue.SkipLine(), "마지막 대사 스킵과 종료 후 중복 거부");
        var ids = new[] { 101, 102 };
        var translated = new TextboxPlayback(ids, id => id == 101 ? "첫 ID의 여러 줄\n내용" : "다음 ID", 30);
        ids[1] = 999;
        check(translated.CurrentTextId == 101 && translated.SkipLine() && translated.CurrentTextId == 102 &&
            translated.CurrentLine == "다음 ID", "줄바꿈과 관계없이 Language ID 하나를 넘기고 원본 ID 목록 변경과 분리");
        var singleVideo = new CutscenePlayback();
        singleVideo.TryPlay(new CutsceneSequence("Mixed", new[] {
            new CutsceneStep(CutsceneMedia.Video, "Video", 0),
            new CutsceneStep(CutsceneMedia.Image, "Image", 1000)
        }));
        long oldVideo = singleVideo.StepToken;
        check(singleVideo.SkipVideo() && singleVideo.StepIndex == 1 && singleVideo.IsRunning,
            "영상 하나 스킵은 컷씬 전체를 끝내지 않음");
        check(!singleVideo.ReportVideoEnded(oldVideo) && !singleVideo.SkipVideo(),
            "이전 영상 종료 콜백과 그림의 영상 스킵 거부");
        check(singleVideo.Skip() && singleVideo.EndReason == CutsceneEndReason.Skipped,
            "다음 구간 준비 중에도 컷씬 전체 즉시 스킵");
        check(CutsceneEndReason.Completed.AllowsProgress() && CutsceneEndReason.Skipped.AllowsProgress()
            && !CutsceneEndReason.Cancelled.AllowsProgress() && !CutsceneEndReason.Failed.AllowsProgress(),
            "컷씬 완료·건너뛰기만 다음 진행을 허용하고 취소·실패는 진행하지 않음");
        var steps = new[] {
            new CutsceneStep(CutsceneMedia.Image, "Ledger01", 4000),
            new CutsceneStep(CutsceneMedia.Image, "EndingCard", 3000, 30025, false)
        };
        var sequence = new CutsceneSequence("Ledger", steps);
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
        check(playback.Skip() && result == CutsceneEndReason.Skipped && finished == 2, "입력 즉시 컷씬 전체 스킵");
        check(!playback.Skip(), "종료된 컷씬의 중복 스킵 거부");

        Start(sequence, ready: true);
        check(playback.Advance() && playback.StepIndex == 1, "그림 다음 장 요청");
        check(playback.Cancel() && result == CutsceneEndReason.Cancelled, "준비 중에도 명시적 취소");

        var video = new CutsceneSequence("Intro", new[] { new CutsceneStep(CutsceneMedia.Video, "IntroVideo", 0) });
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
