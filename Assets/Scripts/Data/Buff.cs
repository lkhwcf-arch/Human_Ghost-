// 자동 생성 데이터 정의입니다. 직접 수정하면 다음 생성 시 덮어씁니다.
namespace HumanGhost.Data.Definitions
{
    public sealed class Buff
    {
        public int ID { get; }
        public string Key { get; }
        public string StatType { get; }
        public int Value { get; }
        public int Duration { get; }
        public int TickInterval { get; }
        public int UseTime { get; }
        public int Charges { get; }
        public bool Stackable { get; }
        public Buff(
            int id,
            string key,
            string statType,
            int value,
            int duration,
            int tickInterval,
            int useTime,
            int charges,
            bool stackable
        )
        {
            ID = id;
            Key = key;
            StatType = statType;
            Value = value;
            Duration = duration;
            TickInterval = tickInterval;
            UseTime = useTime;
            Charges = charges;
            Stackable = stackable;
        }
    }
}
