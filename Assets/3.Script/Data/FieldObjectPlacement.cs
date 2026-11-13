// 최초 생성 데이터 정의입니다. 기존 파일은 자동 갱신하지 않으므로 시트 구조 변경 시 함께 수정하세요.
namespace HumanGhost.Data.Definitions
{
    public sealed class FieldObjectPlacement
    {
        public int ID { get; }
        public int StageID { get; }
        public int PlacementID { get; }
        public int FieldObjectID { get; }
        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int GuideFireID { get; }
        public FieldObjectPlacement(int id, int stageID, int placementID, int fieldObjectID, int x, int y, int width, int guideFireID)
        {
            ID = id;
            StageID = stageID;
            PlacementID = placementID;
            FieldObjectID = fieldObjectID;
            X = x;
            Y = y;
            Width = width;
            GuideFireID = guideFireID;
        }
    }
}
