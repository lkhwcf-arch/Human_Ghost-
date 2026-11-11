using System;

namespace HumanGhost.Gameplay
{
    // 호출자가 시간·입력·준비 결과를 전달합니다. 변경 알림 안에서 재생 명령을 다시 호출하지 않습니다.
    public sealed class CutscenePlayback
    {
        private int _elapsedMs;
        public CutsceneSequence Sequence { get; private set; }
        public int StepIndex { get; private set; } = -1;
        public long StepToken { get; private set; }
        public CutscenePlaybackState State { get; private set; }
        public CutsceneEndReason? EndReason { get; private set; }
        public string FailureMessage { get; private set; }
        public CutsceneStep Current => StepIndex < 0 ? null : Sequence.Steps[StepIndex];
        public bool IsRunning => State == CutscenePlaybackState.Preparing || State == CutscenePlaybackState.Playing;
        public event Action Changed;
        public event Action<CutsceneEndReason> Finished;

        public bool TryPlay(CutsceneSequence sequence)
        {
            if (sequence == null) throw new ArgumentNullException(nameof(sequence));
            if (IsRunning) return false;
            Sequence = sequence;
            StepIndex = -1;
            EndReason = null;
            FailureMessage = null;
            BeginNext();
            return true;
        }

        public bool MarkReady(long token)
        {
            if (!IsCurrent(token) || State != CutscenePlaybackState.Preparing) return false;
            ChangeState(CutscenePlaybackState.Playing);
            return true;
        }

        public bool ReportVideoEnded(long token)
        {
            if (!IsCurrent(token) || State != CutscenePlaybackState.Playing || Current.Media != CutsceneMedia.Video) return false;
            BeginNext();
            return true;
        }

        public bool Advance()
        {
            if (State != CutscenePlaybackState.Playing || !Current.CanAdvance) return false;
            BeginNext();
            return true;
        }

        public bool Skip()
        {
            if (!IsRunning) return false;
            End(CutsceneEndReason.Skipped);
            return true;
        }

        public bool SkipVideo()
        {
            if (!IsRunning || Current.Media != CutsceneMedia.Video) return false;
            BeginNext();
            return true;
        }

        // 준비 시간은 표시 시간에 포함하지 않고, 새 구간이 준비되기 전 남은 프레임 시간을 넘기지 않습니다.
        public void Tick(int milliseconds)
        {
            if (milliseconds < 0) throw new ArgumentOutOfRangeException(nameof(milliseconds));
            if (State != CutscenePlaybackState.Playing || milliseconds == 0) return;
            if (Current.Media == CutsceneMedia.Image)
            {
                _elapsedMs = AccumulateTime(_elapsedMs, milliseconds, Current.DurationMs);
                if (_elapsedMs == Current.DurationMs) { BeginNext(); return; }
            }
        }

        public bool Cancel()
        {
            if (!IsRunning) return false;
            End(CutsceneEndReason.Cancelled);
            return true;
        }

        public bool Fail(long token, string message)
        {
            if (!IsCurrent(token)) return false;
            FailureMessage = message ?? "컷씬 재생에 실패했습니다.";
            End(CutsceneEndReason.Failed);
            return true;
        }

        private void BeginNext()
        {
            if (StepIndex + 1 == Sequence.Steps.Count) { End(CutsceneEndReason.Completed); return; }
            StepIndex++;
            StepToken = checked(StepToken + 1);
            _elapsedMs = 0;
            ChangeState(CutscenePlaybackState.Preparing);
        }

        private void End(CutsceneEndReason reason)
        {
            EndReason = reason;
            ChangeState(CutscenePlaybackState.Ended);
            Finished?.Invoke(reason);
        }

        private bool IsCurrent(long token) => IsRunning && token == StepToken;

        private void ChangeState(CutscenePlaybackState state)
        {
            State = state;
            Changed?.Invoke();
        }

        // 상한까지만 더해 큰 프레임 시간에서도 정수 넘침을 막습니다.
        private static int AccumulateTime(int elapsed, int milliseconds, int limit) =>
            elapsed + Math.Min(milliseconds, limit - elapsed);
    }
}
