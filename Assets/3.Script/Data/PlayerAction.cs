// 최초 생성 데이터 정의입니다. 기존 파일은 자동 갱신하지 않으므로 시트 구조 변경 시 함께 수정하세요.
namespace HumanGhost.Data.Definitions
{
    public sealed class PlayerAction
    {
        public int ID { get; }
        public int PlayerID { get; }
        public int ParryWindow { get; }
        public int ParryStamina { get; }
        public int ParryStunTime { get; }
        public int ParryRecovery { get; }
        public int ParryPoiseDamage { get; }
        public int ParryHitStop { get; }
        public int WeaponSwapTime { get; }
        public PlayerAction(int id, int playerID, int parryWindow, int parryStamina, int parryStunTime, int parryRecovery, int parryPoiseDamage, int parryHitStop, int weaponSwapTime)
        {
            ID = id;
            PlayerID = playerID;
            ParryWindow = parryWindow;
            ParryStamina = parryStamina;
            ParryStunTime = parryStunTime;
            ParryRecovery = parryRecovery;
            ParryPoiseDamage = parryPoiseDamage;
            ParryHitStop = parryHitStop;
            WeaponSwapTime = weaponSwapTime;
        }
    }
}
