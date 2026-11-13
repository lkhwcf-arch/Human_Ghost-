// 최초 생성 데이터 정의입니다. 기존 파일은 자동 갱신하지 않으므로 시트 구조 변경 시 함께 수정하세요.
namespace HumanGhost.Data.Definitions
{
    public sealed class MonsterAggro
    {
        public int ID { get; }
        public int MonsterID { get; }
        public int SightHeight { get; }
        public int LoseRange { get; }
        public int LoseHeight { get; }
        public int LoseTime { get; }
        public int LeashRange { get; }
        public int ReacquireDelay { get; }
        public MonsterAggro(int id, int monsterID, int sightHeight, int loseRange, int loseHeight, int loseTime, int leashRange, int reacquireDelay)
        {
            ID = id;
            MonsterID = monsterID;
            SightHeight = sightHeight;
            LoseRange = loseRange;
            LoseHeight = loseHeight;
            LoseTime = loseTime;
            LeashRange = leashRange;
            ReacquireDelay = reacquireDelay;
        }
    }
}
