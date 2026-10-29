// 자동 생성 데이터 정의입니다. 직접 수정하면 다음 생성 시 덮어씁니다.
namespace HumanGhost.Data.Definitions
{
    public abstract class CharacterStat
    {
        public int HP { get; }
        public int ATK { get; }
        public int DEF { get; }
        public int Dodge { get; }
        public int Crit { get; }
        public int CritMult { get; }
        public int Poise { get; }
        public int MoveSpeed { get; }
        public int ArmorPen { get; }
        protected CharacterStat(
            int hp,
            int atk,
            int def,
            int dodge,
            int crit,
            int critMult,
            int poise,
            int moveSpeed,
            int armorPen
        )
        {
            HP = hp;
            ATK = atk;
            DEF = def;
            Dodge = dodge;
            Crit = crit;
            CritMult = critMult;
            Poise = poise;
            MoveSpeed = moveSpeed;
            ArmorPen = armorPen;
        }
        protected CharacterStat(CharacterStat source) : this(source.HP, source.ATK, source.DEF, source.Dodge, source.Crit, source.CritMult, source.Poise, source.MoveSpeed, source.ArmorPen) { }
    }
}
