// 자동 생성 파일입니다. 원본 시트를 수정한 뒤 다시 배포하세요.
namespace HumanGhost.Data.Definitions
{
    public sealed class BossState
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
        public int Phase { get; }
        public string Key { get; }
        public int HPThreshold { get; }
        public BossState(int id, int unitID, int idleMin, int idleMax, int poiseRegenDelay, int poiseRegen, int exposeTime, string executeType, int executeDamage, bool superArmor, int phase, string key, int hPThreshold)
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
            Phase = phase;
            Key = key;
            HPThreshold = hPThreshold;
        }
    }
}
