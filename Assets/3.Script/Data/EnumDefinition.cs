// 최초 생성 데이터 정의입니다. 기존 파일은 자동 갱신하지 않으므로 시트 구조 변경 시 함께 수정하세요.
namespace HumanGhost.Data.Definitions
{
    public sealed class EnumDefinition
    {
        public int ID { get; }
        public string EnumName { get; }
        public string Key { get; }
        public int Value { get; }
        public string SheetKey { get; }
        public EnumDefinition(int id, string enumName, string key, int value, string sheetKey)
        {
            ID = id;
            EnumName = enumName;
            Key = key;
            Value = value;
            SheetKey = sheetKey;
        }
    }
}
