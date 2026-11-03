// 최초 생성 데이터 정의입니다. 기존 파일은 자동 갱신하지 않으므로 시트 구조 변경 시 함께 수정하세요.
namespace HumanGhost.Data.Definitions
{
    public sealed class CutsceneStep
    {
        public int ID { get; }
        public int CutsceneID { get; }
        public int Order { get; }
        public string MediaType { get; }
        public string ResourceAddress { get; }
        public int DurationMs { get; }
        public int? TextID { get; }
        public bool CanAdvance { get; }
        public CutsceneStep(int id, int cutsceneID, int order, string mediaType, string resourceAddress, int durationMs, int? textID, bool canAdvance)
        {
            ID = id;
            CutsceneID = cutsceneID;
            Order = order;
            MediaType = mediaType;
            ResourceAddress = resourceAddress;
            DurationMs = durationMs;
            TextID = textID;
            CanAdvance = canAdvance;
        }
    }
}
