// 최초 생성 데이터 정의입니다. 기존 파일은 자동 갱신하지 않으므로 시트 구조 변경 시 함께 수정하세요.
namespace HumanGhost.Data.Definitions
{
    public sealed class StatusEffect
    {
        public int ID { get; }
        public string Category { get; }
        public int Value { get; }
        public int Duration { get; }
        public int TickInterval { get; }
        public bool Stackable { get; }
        public int TextID { get; }
        public StatusEffect(int id, string category, int value, int duration, int tickInterval, bool stackable, int textID)
        {
            ID = id;
            Category = category;
            Value = value;
            Duration = duration;
            TickInterval = tickInterval;
            Stackable = stackable;
            TextID = textID;
        }
    }
}
