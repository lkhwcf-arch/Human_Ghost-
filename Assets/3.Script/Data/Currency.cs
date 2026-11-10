// 최초 생성 데이터 정의입니다. 기존 파일은 자동 갱신하지 않으므로 시트 구조 변경 시 함께 수정하세요.
namespace HumanGhost.Data.Definitions
{
    public sealed class Currency
    {
        public int ID { get; }
        public int TextID { get; }
        public int DeathLossRate { get; }
        public bool ContentEnabled { get; }
        public Currency(int id, int textID, int deathLossRate, bool contentEnabled)
        {
            ID = id;
            TextID = textID;
            DeathLossRate = deathLossRate;
            ContentEnabled = contentEnabled;
        }
    }
}
