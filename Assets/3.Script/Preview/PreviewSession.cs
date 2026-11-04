using System.Collections.Generic;

namespace HumanGhost.Gameplay.Preview
{
    // 확인 장면 전용 씬 간 임시 상태입니다. 진행 상태 단일 소유자가 정해지면 그쪽으로 옮기고 이 클래스를 지웁니다.
    internal static class PreviewSession
    {
        public static StageProgress Progress = new StageProgress();
        public static readonly HashSet<string> OpenedGates = new HashSet<string>();
        public static readonly HashSet<int> DefeatedBoss = new HashSet<int>();
        public static int CurrentStage = StageExitRules.VillageStage;
        public static bool CheckpointPassed;
        public static string ArrivalAnchor;

        // 새로 시작입니다. 인트로 뒤 맵 1 시작 지점입니다.
        public static void Reset()
        {
            Progress = new StageProgress();
            OpenedGates.Clear();
            DefeatedBoss.Clear();
            CurrentStage = StageExitRules.VillageStage;
            CheckpointPassed = false;
            ArrivalAnchor = null;
        }
    }
}
