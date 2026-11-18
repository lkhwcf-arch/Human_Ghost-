// 최초 생성 데이터 정의입니다. 기존 파일은 자동 갱신하지 않으므로 시트 구조 변경 시 함께 수정하세요.
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
        public bool ContentEnabled { get; }
        public int UnlockConditionType { get; }
        public int UnlockTargetID { get; }
        public int UnlockRequiredValue { get; }
        public int? AmbienceSoundID { get; }
        public Stage(int id, int textID, int order, int? nextStage, string bGM_Normal, string bGM_Battle, string spawnFile, bool active, bool contentEnabled, int unlockConditionType, int unlockTargetID, int unlockRequiredValue, int? ambienceSoundID = null)
        {
            ID = id;
            TextID = textID;
            Order = order;
            NextStage = nextStage;
            BGM_Normal = bGM_Normal;
            BGM_Battle = bGM_Battle;
            SpawnFile = spawnFile;
            Active = active;
            ContentEnabled = contentEnabled;
            UnlockConditionType = unlockConditionType;
            UnlockTargetID = unlockTargetID;
            UnlockRequiredValue = unlockRequiredValue;
            AmbienceSoundID = ambienceSoundID;
        }
    }
}
