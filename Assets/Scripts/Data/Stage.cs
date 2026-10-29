// 자동 생성 데이터 정의입니다. 직접 수정하면 다음 생성 시 덮어씁니다.
namespace HumanGhost.Data.Definitions
{
    public sealed class Stage
    {
        public int ID { get; }
        public int TextID { get; }
        public int Order { get; }
        public int? NextStage { get; }
        public string BGM_Normal { get; }
        public string BGM_Battle { get; }
        public string SpawnFile { get; }
        public bool Active { get; }
        public Stage(
            int id,
            int textID,
            int order,
            int? nextStage,
            string bGM_Normal,
            string bGM_Battle,
            string spawnFile,
            bool active
        )
        {
            ID = id;
            TextID = textID;
            Order = order;
            NextStage = nextStage;
            BGM_Normal = bGM_Normal;
            BGM_Battle = bGM_Battle;
            SpawnFile = spawnFile;
            Active = active;
        }
    }
}
