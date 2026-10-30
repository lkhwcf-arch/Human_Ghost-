// 자동 생성 파일입니다. 원본 시트를 수정한 뒤 다시 배포하세요.
namespace HumanGhost.Data.Definitions
{
    public sealed class Item
    {
        public int ID { get; }
        public int? TextID { get; }
        public string ItemType { get; }
        public string StatJson { get; }
        public bool InGstar { get; }
        public int? SkillID { get; }
        public string Acquire { get; }
        public string AcquireAnim { get; }
        public Item(int id, int? textID, string itemType, string statJson, bool inGstar, int? skillID, string acquire, string acquireAnim)
        {
            ID = id;
            TextID = textID;
            ItemType = itemType;
            StatJson = statJson;
            InGstar = inGstar;
            SkillID = skillID;
            Acquire = acquire;
            AcquireAnim = acquireAnim;
        }
    }
}
