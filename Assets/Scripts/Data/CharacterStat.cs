// 자동 생성 파일입니다. 원본 시트를 수정한 뒤 다시 배포하세요.
namespace HumanGhost.Data.Definitions
{
    public abstract class CharacterStat
    {
        public int HP { get; }
        public int ATK { get; }
        public int DEF { get; }
        public int Dodge { get; }
        public int Crit { get; }
        public int CritDamage { get; }
        public int Poise { get; }
        public int MoveSpeed { get; }
        public int ArmorPen { get; }
        protected CharacterStat(int hp, int atk, int def, int dodge, int crit, int critDamage, int poise, int moveSpeed, int armorPen)
        {
            HP = hp;
            ATK = atk;
            DEF = def;
            Dodge = dodge;
            Crit = crit;
            CritDamage = critDamage;
            Poise = poise;
            MoveSpeed = moveSpeed;
            ArmorPen = armorPen;
        }
    }
}
