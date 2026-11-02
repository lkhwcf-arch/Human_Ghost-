// 최초 생성 데이터 정의입니다. 기존 파일은 자동 갱신하지 않으므로 시트 구조 변경 시 함께 수정하세요.
namespace HumanGhost.Data.Definitions
{
    public sealed class HealthDisplayConfig
    {
        public int ID { get; }
        public string PrefabKey { get; }
        public string AnchorName { get; }
        public int Width { get; }
        public int Height { get; }
        public bool Visible { get; }
        public HealthDisplayConfig(int id, string prefabKey, string anchorName, int width, int height, bool visible)
        {
            ID = id;
            PrefabKey = prefabKey;
            AnchorName = anchorName;
            Width = width;
            Height = height;
            Visible = visible;
        }
    }
}
