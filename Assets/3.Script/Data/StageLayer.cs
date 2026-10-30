// 자동 생성 파일입니다. 원본 시트를 수정한 뒤 다시 배포하세요.
namespace HumanGhost.Data.Definitions
{
    public sealed class StageLayer
    {
        public int ID { get; }
        public int StageID { get; }
        public int LayerIndex { get; }
        public string LayerName { get; }
        public string ImageKey { get; }
        public int Parallax { get; }
        public int SortOrder { get; }
        public StageLayer(int id, int stageID, int layerIndex, string layerName, string imageKey, int parallax, int sortOrder)
        {
            ID = id;
            StageID = stageID;
            LayerIndex = layerIndex;
            LayerName = layerName;
            ImageKey = imageKey;
            Parallax = parallax;
            SortOrder = sortOrder;
        }
    }
}
