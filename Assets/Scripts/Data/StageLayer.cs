// 자동 생성 데이터 정의입니다. 직접 수정하면 다음 생성 시 덮어씁니다.
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
        public StageLayer(
            int id,
            int stageID,
            int layerIndex,
            string layerName,
            string imageKey,
            int parallax,
            int sortOrder
        )
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
