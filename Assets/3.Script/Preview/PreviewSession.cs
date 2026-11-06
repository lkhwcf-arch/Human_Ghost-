using System.Collections.Generic;

namespace HumanGhost.Gameplay.Preview
{
    // 확인 장면 전용 씬 간 임시 상태입니다. 진행 상태 단일 소유자가 정해지면 그쪽으로 옮기고 이 클래스를 지웁니다.
    internal static class PreviewSession
    {
        public static StageProgress Progress = new StageProgress();
        public static readonly HashSet<int> OpenedGates = new HashSet<int>();
        public static readonly HashSet<int> DefeatedBoss = new HashSet<int>();
        public static readonly HashSet<MonsterSpawnId> DefeatedMonsters = new HashSet<MonsterSpawnId>();
        public static int CurrentStage = StageExitRules.VillageStage;
        public static bool CheckpointPassed;
        public static int ShrineId;
        public static readonly HashSet<int> ActiveShrines = new HashSet<int>();
        public static readonly HashSet<int> ViewedCutscenes = new HashSet<int>();
        public static string ArrivalAnchor;
        // 맵 이동으로 씬을 다시 불러와도 이어 갈 값입니다. 사망 재시작은 비워서 가득 채웁니다(K16add).
        public static int? CarriedHealth;
        public static int CarriedHerbs;

        // 새로 시작입니다. 인트로 뒤 맵 1 시작 지점입니다.
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            Progress = new StageProgress();
            OpenedGates.Clear();
            DefeatedBoss.Clear();
            DefeatedMonsters.Clear();
            CurrentStage = StageExitRules.VillageStage;
            CheckpointPassed = false;
            ShrineId = 0;
            ActiveShrines.Clear();
            ViewedCutscenes.Clear();
            ArrivalAnchor = null;
            CarriedHealth = null;
            CarriedHerbs = 0;
        }
    }
}
