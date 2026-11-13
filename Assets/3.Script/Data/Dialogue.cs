// 최초 생성 데이터 정의입니다. 기존 파일은 자동 갱신하지 않으므로 시트 구조 변경 시 함께 수정하세요.
namespace HumanGhost.Data.Definitions
{
    public sealed class Dialogue
    {
        public int ID { get; }
        public string TriggerType { get; }
        public int TriggerTargetID { get; }
        public string RepeatMode { get; }
        public bool PauseGame { get; }
        public int ObjectiveTextID { get; }
        public string LineFile { get; }
        public Dialogue(int id, string triggerType, int triggerTargetID, string repeatMode, bool pauseGame, int objectiveTextID, string lineFile)
        {
            ID = id;
            TriggerType = triggerType;
            TriggerTargetID = triggerTargetID;
            RepeatMode = repeatMode;
            PauseGame = pauseGame;
            ObjectiveTextID = objectiveTextID;
            LineFile = lineFile;
        }
    }
}
