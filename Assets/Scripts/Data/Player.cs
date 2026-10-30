// 자동 생성 파일입니다. 원본 시트를 수정한 뒤 다시 배포하세요.
namespace HumanGhost.Data.Definitions
{
    public sealed class Player : CharacterStat
    {
        public int ID { get; }
        public int UnitID { get; }
        public int StaminaMax { get; }
        public int StaminaRegen { get; }
        public int StaminaRegenDelay { get; }
        public int RollDistance { get; }
        public int RollTime { get; }
        public int RollIFrame { get; }
        public int RollStamina { get; }
        public int JumpHeight { get; }
        public int HitStunTime { get; }
        public int HitIFrame { get; }
        public int CritHitStunTime { get; }
        public int LandFallHeight { get; }
        public int LandTime { get; }
        public bool HangAuto { get; }
        public int InputBuffer { get; }
        public int StartWeapon { get; }
        public Player(UnitStat common, int id, int unitID, int staminaMax, int staminaRegen, int staminaRegenDelay, int rollDistance, int rollTime, int rollIFrame, int rollStamina, int jumpHeight, int hitStunTime, int hitIFrame, int critHitStunTime, int landFallHeight, int landTime, bool hangAuto, int inputBuffer, int startWeapon)
            : base(common.HP, common.ATK, common.DEF, common.Dodge, common.Crit, common.CritDamage, common.Poise, common.MoveSpeed, common.ArmorPen)
        {
            ID = id;
            UnitID = unitID;
            StaminaMax = staminaMax;
            StaminaRegen = staminaRegen;
            StaminaRegenDelay = staminaRegenDelay;
            RollDistance = rollDistance;
            RollTime = rollTime;
            RollIFrame = rollIFrame;
            RollStamina = rollStamina;
            JumpHeight = jumpHeight;
            HitStunTime = hitStunTime;
            HitIFrame = hitIFrame;
            CritHitStunTime = critHitStunTime;
            LandFallHeight = landFallHeight;
            LandTime = landTime;
            HangAuto = hangAuto;
            InputBuffer = inputBuffer;
            StartWeapon = startWeapon;
        }
    }
}
