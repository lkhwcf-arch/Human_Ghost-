// 최초 생성 데이터 정의입니다. 기존 파일은 자동 갱신하지 않으므로 시트 구조 변경 시 함께 수정하세요.
namespace HumanGhost.Data.Definitions
{
    public sealed class MonsterAggro
    {
        public int ID { get; }
        public int MonsterID { get; }
        public int FrontRange { get; }
        public int BackRange { get; }
        public int UpRange { get; }
        public int DownRange { get; }
        public int FrontUpRange { get; }
        public int BackUpRange { get; }
        public int FrontDownRange { get; }
        public int BackDownRange { get; }
        public int LoseRange { get; }
        public int LoseHeight { get; }
        public int LoseTime { get; }
        public int LeashRange { get; }
        public int ReacquireDelay { get; }
        public int SameLevelHeight { get; }
        public MonsterAggro(int id, int monsterID, int frontRange, int backRange, int upRange, int downRange,
            int frontUpRange, int backUpRange, int frontDownRange, int backDownRange,
            int loseRange, int loseHeight, int loseTime, int leashRange, int reacquireDelay, int sameLevelHeight)
        {
            ID = id;
            MonsterID = monsterID;
            FrontRange = frontRange;
            BackRange = backRange;
            UpRange = upRange;
            DownRange = downRange;
            FrontUpRange = frontUpRange;
            BackUpRange = backUpRange;
            FrontDownRange = frontDownRange;
            BackDownRange = backDownRange;
            LoseRange = loseRange;
            LoseHeight = loseHeight;
            LoseTime = loseTime;
            LeashRange = leashRange;
            ReacquireDelay = reacquireDelay;
            SameLevelHeight = sameLevelHeight;
        }
    }
}
