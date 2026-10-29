// 자동 생성 데이터 정의입니다. 직접 수정하면 다음 생성 시 덮어씁니다.
namespace HumanGhost.Data.Definitions
{
    public sealed class Boss : CharacterStat
    {
        public int ID { get; }
        public System.Collections.Generic.IReadOnlyList<BossState> States { get; }
        public Boss(
            UnitStat common,
            BossState[] states
        ) : base(common)
        {
            ID = common.ID;
            States = System.Array.AsReadOnly((BossState[])states.Clone());
        }
    }
}
