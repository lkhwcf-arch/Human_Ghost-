// 최초 생성 데이터 정의입니다. 기존 파일은 자동 갱신하지 않으므로 시트 구조 변경 시 함께 수정하세요.
namespace HumanGhost.Data.Definitions
{
    public sealed class Npc
    {
        public int ID { get; }
        public int NameTextID { get; }
        public string Type { get; }
        public string Prefab { get; }
        public int DialogueID { get; }
        public int ShopID { get; }
        public bool ContentEnabled { get; }
        public Npc(int id, int nameTextID, string type, string prefab, int dialogueID, int shopID, bool contentEnabled)
        {
            ID = id;
            NameTextID = nameTextID;
            Type = type;
            Prefab = prefab;
            DialogueID = dialogueID;
            ShopID = shopID;
            ContentEnabled = contentEnabled;
        }
    }
}
