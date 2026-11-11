// 최초 생성 데이터 정의입니다. 기존 파일은 자동 갱신하지 않으므로 시트 구조 변경 시 함께 수정하세요.
namespace HumanGhost.Data.Definitions
{
    public sealed class MonsterState
    {
        public int ID { get; }
        public int UnitID { get; }
        public string Category { get; }
        public bool Immune { get; }
        public int Duration { get; }
        public MonsterState(int id, int unitID, string category, bool immune, int duration)
        {
            ID = id;
            UnitID = unitID;
            Category = category;
            Immune = immune;
            Duration = duration;
        }
    }
}
