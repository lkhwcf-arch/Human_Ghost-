// 자동 생성 데이터 정의입니다. 직접 수정하면 다음 생성 시 덮어씁니다.
namespace HumanGhost.Data.Definitions
{
    public sealed class PlayerState
    {
        public int ID { get; }
        public int UnitID { get; }
        public string Key { get; }
        public int StateID { get; }
        public int Duration { get; }
        public int TickInterval { get; }
        public int TickDamage { get; }
        public bool BlockInput { get; }
        public bool Stackable { get; }
        public PlayerState(
            int id,
            int unitID,
            string key,
            int stateID,
            int duration,
            int tickInterval,
            int tickDamage,
            bool blockInput,
            bool stackable
        )
        {
            ID = id;
            UnitID = unitID;
            Key = key;
            StateID = stateID;
            Duration = duration;
            TickInterval = tickInterval;
            TickDamage = tickDamage;
            BlockInput = blockInput;
            Stackable = stackable;
        }
    }
}
