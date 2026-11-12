// 최초 생성 데이터 정의입니다. 기존 파일은 자동 갱신하지 않으므로 시트 구조 변경 시 함께 수정하세요.
namespace HumanGhost.Data.Definitions
{
    public sealed class ExecuteReward
    {
        public int ID { get; }
        public int MonsterID { get; }
        public int CoinDrop { get; }
        public int HealHP { get; }
        public int Corruption { get; }
        public int DropItemID { get; }
        public ExecuteReward(int id, int monsterID, int coinDrop, int healHP, int corruption, int dropItemID)
        {
            ID = id;
            MonsterID = monsterID;
            CoinDrop = coinDrop;
            HealHP = healHP;
            Corruption = corruption;
            DropItemID = dropItemID;
        }
    }
}
