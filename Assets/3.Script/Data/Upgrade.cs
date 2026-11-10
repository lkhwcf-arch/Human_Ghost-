// 최초 생성 데이터 정의입니다. 기존 파일은 자동 갱신하지 않으므로 시트 구조 변경 시 함께 수정하세요.
namespace HumanGhost.Data.Definitions
{
    public sealed class Upgrade
    {
        public int ID { get; }
        public int TextID { get; }
        public int CurrencyID { get; }
        public string Target { get; }
        public int Step { get; }
        public int Value { get; }
        public int Price { get; }
        public bool ContentEnabled { get; }
        public Upgrade(int id, int textID, int currencyID, string target, int step, int value, int price, bool contentEnabled)
        {
            ID = id;
            TextID = textID;
            CurrencyID = currencyID;
            Target = target;
            Step = step;
            Value = value;
            Price = price;
            ContentEnabled = contentEnabled;
        }
    }
}
