// 자동 생성 파일입니다. 원본 시트를 수정한 뒤 다시 배포하세요.
namespace HumanGhost.Data.Definitions
{
    public sealed class Skill
    {
        public int ID { get; }
        public int TextID { get; }
        public int WeaponID { get; }
        public int StateID { get; }
        public int Damage { get; }
        public int PoiseDamage { get; }
        public int Cooldown { get; }
        public string UnlockType { get; }
        public string Effect { get; }
        public bool ContentEnabled { get; }
        public int UnlockConditionType { get; }
        public int UnlockTargetID { get; }
        public int UnlockRequiredValue { get; }
        public Skill(int id, int textID, int weaponID, int stateID, int damage, int poiseDamage, int cooldown, string unlockType, string effect, bool contentEnabled, int unlockConditionType, int unlockTargetID, int unlockRequiredValue)
        {
            ID = id;
            TextID = textID;
            WeaponID = weaponID;
            StateID = stateID;
            Damage = damage;
            PoiseDamage = poiseDamage;
            Cooldown = cooldown;
            UnlockType = unlockType;
            Effect = effect;
            ContentEnabled = contentEnabled;
            UnlockConditionType = unlockConditionType;
            UnlockTargetID = unlockTargetID;
            UnlockRequiredValue = unlockRequiredValue;
        }
    }
}
