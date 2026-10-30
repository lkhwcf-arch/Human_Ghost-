// 자동 생성 파일입니다. 원본 시트를 수정한 뒤 다시 배포하세요.
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
        public PlayerState(int id, int unitID, string key, int stateID, int duration, int tickInterval, int tickDamage, bool blockInput, bool stackable)
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
