// 자동 생성 파일입니다. 원본 시트를 수정한 뒤 다시 배포하세요.
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
        public Monster(UnitStat common, int id, int unitID, int idleMin, int idleMax, int poiseRegenDelay, int poiseRegen, int exposeTime, string executeType, int executeDamage, bool superArmor, string aIType, int sightRange, int attackRange)
            : base(common.HP, common.ATK, common.DEF, common.Dodge, common.Crit, common.CritDamage, common.Poise, common.MoveSpeed, common.ArmorPen)
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
