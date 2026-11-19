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
        public bool ContentEnabled { get; }
        public string EffectImmunities { get; }
        // 피격 경직과 죽을 때 남기는 불(11/19 패턴 설정 파일에서 이전, ms·틱당 피해·절반 폭 tile_x100). 불 시간이 0이면 남기지 않습니다.
        public int HitStun { get; }
        public int CritHitStun { get; }
        public int DeathFireTime { get; }
        public int DeathFireDamage { get; }
        public int DeathFireHalfWidth { get; }
        public int DeathFireTick { get; }
        public Monster(UnitStat common, int id, int unitID, int idleMin, int idleMax, int poiseRegenDelay, int poiseRegen, int exposeTime, string executeType, int executeDamage, bool superArmor, string aIType, int sightRange, int attackRange, bool contentEnabled, string effectImmunities = "", int hitStun = 0, int critHitStun = 0, int deathFireTime = 0, int deathFireDamage = 0, int deathFireHalfWidth = 0, int deathFireTick = 0)
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
            ContentEnabled = contentEnabled;
            EffectImmunities = effectImmunities ?? "";
            HitStun = hitStun;
            CritHitStun = critHitStun;
            DeathFireTime = deathFireTime;
            DeathFireDamage = deathFireDamage;
            DeathFireHalfWidth = deathFireHalfWidth;
            DeathFireTick = deathFireTick;
        }
    }
}
