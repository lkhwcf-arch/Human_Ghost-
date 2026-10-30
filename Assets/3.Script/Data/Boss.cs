// 자동 생성 파일입니다. 원본 시트를 수정한 뒤 다시 배포하세요.
namespace HumanGhost.Data.Definitions
{
    public sealed class Boss : CharacterStat
    {
        public int ID { get; }
        public System.Collections.Generic.IReadOnlyList<BossState> States { get; }
        public Boss(UnitStat common, System.Collections.Generic.IEnumerable<BossState> states)
            : base(common.HP, common.ATK, common.DEF, common.Dodge, common.Crit, common.CritDamage, common.Poise, common.MoveSpeed, common.ArmorPen)
        {
            ID = common.ID;
            States = System.Array.AsReadOnly(System.Linq.Enumerable.ToArray(states));
        }
    }
}
