// 자동 생성 파일입니다. 원본 시트를 수정한 뒤 다시 배포하세요.
namespace HumanGhost.Data.Definitions
{
    public sealed class UnitStat : CharacterStat
    {
        public int ID { get; }
        public int TextID { get; }
        public string Type { get; }
        public int? Tier { get; }
        public UnitStat(int id, int textID, string type, int? tier, int hp, int atk, int def, int dodge, int crit, int critDamage, int poise, int moveSpeed, int armorPen)
            : base(hp, atk, def, dodge, crit, critDamage, poise, moveSpeed, armorPen)
        {
            ID = id;
            TextID = textID;
            Type = type;
            Tier = tier;
        }
    }
}
