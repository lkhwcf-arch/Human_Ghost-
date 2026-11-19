// 최초 생성 데이터 정의입니다. 기존 파일은 자동 갱신하지 않으므로 시트 구조 변경 시 함께 수정하세요.
namespace HumanGhost.Data.Definitions
{
    public sealed class Currency
    {
        public int ID { get; }
        public int TextID { get; }
        public int Category { get; }
        public int DeathLossRate { get; }
        public bool ContentEnabled { get; }
        // 처음 얻었을 때 한 번 띄울 안내 문구와 그 안내의 그림 키(SOUL·COIN, 11/19 추가)입니다.
        public int? FirstNoticeTextID { get; }
        public string NoticeIcon { get; }
        public Currency(int id, int textID, int deathLossRate, bool contentEnabled, int category = 0, int? firstNoticeTextID = null, string noticeIcon = "")
        {
            ID = id;
            TextID = textID;
            Category = category;
            DeathLossRate = deathLossRate;
            ContentEnabled = contentEnabled;
            FirstNoticeTextID = firstNoticeTextID;
            NoticeIcon = noticeIcon ?? "";
        }
    }
}
