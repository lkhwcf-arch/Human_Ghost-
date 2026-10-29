// 자동 생성 데이터 정의입니다. 직접 수정하면 다음 생성 시 덮어씁니다.
namespace HumanGhost.Data.Definitions
{
    public sealed class Elite : CharacterStat
    {
        public int ID { get; }
        public System.Collections.Generic.IReadOnlyList<EliteState> States { get; }
        public Elite(
            UnitStat common,
            EliteState[] states
        ) : base(common)
        {
            ID = common.ID;
            States = System.Array.AsReadOnly((EliteState[])states.Clone());
        }
    }
}
