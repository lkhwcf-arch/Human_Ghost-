namespace HumanGhost.Gameplay
{
    public enum DemoEndingPhase { Waiting, Playing, Exploring, Finishing, Finished, Failed }

    // 영상 재생과 맵 탐색·시연 종료의 순서만 소유합니다. 화면·입력·저장·씬 이동은 호출자가 연결합니다.
    public class DemoEndingFlow
    {
        public DemoEndingPhase Phase { get; private set; }
        public bool CanFinish => Phase == DemoEndingPhase.Exploring;
        public DemoEndingFlow(bool alreadyViewed) => Phase = alreadyViewed ? DemoEndingPhase.Exploring : DemoEndingPhase.Waiting;
        public bool TryBegin(bool bossDefeated)
        {
            if (!bossDefeated || Phase != DemoEndingPhase.Waiting) return false;
            Phase = DemoEndingPhase.Playing;
            return true;
        }
        public void EndMovie(CutsceneEndReason reason)
        {
            if (Phase != DemoEndingPhase.Playing) return;
            Phase = reason.AllowsProgress() ? DemoEndingPhase.Exploring : DemoEndingPhase.Failed;
        }
        public bool TryFinish()
        {
            if (!CanFinish) return false;
            Phase = DemoEndingPhase.Finishing;
            return true;
        }
        public bool Complete()
        {
            if (Phase != DemoEndingPhase.Finishing) return false;
            Phase = DemoEndingPhase.Finished;
            return true;
        }
        public void Fail() => Phase = DemoEndingPhase.Failed;
    }
}
