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
        // 2페이즈 패턴 선택 기준(11/19 코드 상수에서 이전). 쓰지 않는 페이즈는 0입니다.
        public int DesperationHPRate { get; }
        public int CloseRange { get; }
        public int MidRange { get; }
        public int PlatformWaitTime { get; }
        public int SlamAfterCount { get; }
        public int ChargeGap { get; }
        public int LeapAirTime { get; }
        // 이 페이즈의 보스 배경음(GameAudio 키, 비우면 없음, 11/19 코드 키에서 이전)입니다.
        public string Music { get; }
        public BossState(int id, int unitID, int idleMin, int idleMax, int poiseRegenDelay, int poiseRegen, int exposeTime, string executeType, int executeDamage, bool superArmor, int phase, string key, int hPThreshold, int desperationHPRate = 0, int closeRange = 0, int midRange = 0, int platformWaitTime = 0, int slamAfterCount = 0, int chargeGap = 0, int leapAirTime = 0, string music = "")
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
            DesperationHPRate = desperationHPRate;
            CloseRange = closeRange;
            MidRange = midRange;
            PlatformWaitTime = platformWaitTime;
            SlamAfterCount = slamAfterCount;
            ChargeGap = chargeGap;
            LeapAirTime = leapAirTime;
            Music = music ?? "";
        }
    }
}
