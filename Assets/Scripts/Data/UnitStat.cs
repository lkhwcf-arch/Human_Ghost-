// 자동 생성 데이터 정의입니다. 직접 수정하면 다음 생성 시 덮어씁니다.
namespace HumanGhost.Data.Definitions
{
    public sealed class UnitStat : CharacterStat
    {
        public int ID { get; }
        public int TextID { get; }
        public string Type { get; }
        public int? Tier { get; }
        public UnitStat(
            int id,
            int textID,
            string type,
            int? tier,
            int hp,
            int atk,
            int def,
            int dodge,
            int crit,
            int critMult,
            int poise,
            int moveSpeed,
            int armorPen
        ) : base(hp, atk, def, dodge, crit, critMult, poise, moveSpeed, armorPen)
        {
            ID = id;
            TextID = textID;
            Type = type;
            Tier = tier;
        }
    }
}
