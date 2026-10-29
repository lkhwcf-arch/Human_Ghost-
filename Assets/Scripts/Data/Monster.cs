// 자동 생성 데이터 정의입니다. 직접 수정하면 다음 생성 시 덮어씁니다.
namespace HumanGhost.Data.Definitions
{
    public sealed class Monster : CharacterStat
    {
        public int ID { get; }
        public int UnitID { get; }
        public int IdleMin { get; }
        public int IdleMax { get; }
        public int PoiseRegenDelay { get; }
        public int PoiseRegen { get; }
        public int ExposeTime { get; }
        public string ExecuteType { get; }
        public int ExecuteDamage { get; }
        public bool SuperArmor { get; }
        public string AIType { get; }
        public int SightRange { get; }
        public int AttackRange { get; }
        public Monster(
            UnitStat common,
            int id,
            int unitID,
            int idleMin,
            int idleMax,
            int poiseRegenDelay,
            int poiseRegen,
            int exposeTime,
            string executeType,
            int executeDamage,
            bool superArmor,
            string aIType,
            int sightRange,
            int attackRange
        ) : base(common)
        {
            ID = id;
            UnitID = unitID;
            IdleMin = idleMin;
            IdleMax = idleMax;
            PoiseRegenDelay = poiseRegenDelay;
            PoiseRegen = poiseRegen;
            ExposeTime = exposeTime;
            ExecuteType = executeType;
            ExecuteDamage = executeDamage;
            SuperArmor = superArmor;
            AIType = aIType;
            SightRange = sightRange;
            AttackRange = attackRange;
        }
    }
}
