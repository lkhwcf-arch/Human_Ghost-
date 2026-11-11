// 최초 생성 데이터 정의입니다. 기존 파일은 자동 갱신하지 않으므로 시트 구조 변경 시 함께 수정하세요.
namespace HumanGhost.Data.Definitions
{
    public sealed class MonsterSkill
    {
        public int ID { get; }
        public int MonsterID { get; }
        public int StateID { get; }
        public int EffectID { get; }
        public MonsterSkill(int id, int monsterID, int stateID, int effectID)
        {
            ID = id;
            MonsterID = monsterID;
            StateID = stateID;
            EffectID = effectID;
        }
    }
}
