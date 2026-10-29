// 자동 생성 데이터 정의입니다. 직접 수정하면 다음 생성 시 덮어씁니다.
namespace HumanGhost.Data.Definitions
{
    public sealed class Skill
    {
        public int ID { get; }
        public int TextID { get; }
        public int WeaponID { get; }
        public int StateID { get; }
        public int DamageMult { get; }
        public int PoiseDamage { get; }
        public int Cooldown { get; }
        public string UnlockType { get; }
        public string Effect { get; }
        public Skill(
            int id,
            int textID,
            int weaponID,
            int stateID,
            int damageMult,
            int poiseDamage,
            int cooldown,
            string unlockType,
            string effect
        )
        {
            ID = id;
            TextID = textID;
            WeaponID = weaponID;
            StateID = stateID;
            DamageMult = damageMult;
            PoiseDamage = poiseDamage;
            Cooldown = cooldown;
            UnlockType = unlockType;
            Effect = effect;
        }
    }
}
