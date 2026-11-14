// 최초 생성 데이터 정의입니다. 기존 파일은 자동 갱신하지 않으므로 시트 구조 변경 시 함께 수정하세요.
namespace HumanGhost.Data.Definitions
{
    public sealed class StageExit
    {
        public int ID { get; }
        public int StageID { get; }
        public string TriggerType { get; }
        public string AnchorName { get; }
        public int TargetStageID { get; }
        public string ArrivalAnchorName { get; }
        public int RangeX { get; }
        public int RangeY { get; }
        public int PromptTextID { get; }
        public int BlockedTextID { get; }
        public bool ContentEnabled { get; }
        public StageExit(int id, int stageID, string triggerType, string anchorName, int targetStageID, string arrivalAnchorName, int rangeX, int rangeY, int promptTextID, int blockedTextID, bool contentEnabled)
        {
            ID = id;
            StageID = stageID;
            TriggerType = triggerType;
            AnchorName = anchorName;
            TargetStageID = targetStageID;
            ArrivalAnchorName = arrivalAnchorName;
            RangeX = rangeX;
            RangeY = rangeY;
            PromptTextID = promptTextID;
            BlockedTextID = blockedTextID;
            ContentEnabled = contentEnabled;
        }
    }
}
