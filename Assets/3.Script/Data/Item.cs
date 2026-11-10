// 자동 생성 파일입니다. 원본 시트를 수정한 뒤 다시 배포하세요.
namespace HumanGhost.Data.Definitions
{
    public sealed class Item
    {
        public int ID { get; }
        public int? TextID { get; }
        public string ItemType { get; }
        public int Category { get; }
        public string StatJson { get; }
        public int? SkillID { get; }
        public string Acquire { get; }
        public string AcquireAnim { get; }
        public bool ContentEnabled { get; }
        public Item(int id, int? textID, string itemType, string statJson, int? skillID, string acquire, string acquireAnim, bool contentEnabled, int category = 0)
        {
            ID = id;
            TextID = textID;
            ItemType = itemType;
            Category = category;
            StatJson = statJson;
            SkillID = skillID;
            Acquire = acquire;
            AcquireAnim = acquireAnim;
            ContentEnabled = contentEnabled;
        }
    }
}
