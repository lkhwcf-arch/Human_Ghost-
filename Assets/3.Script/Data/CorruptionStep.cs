// 최초 생성 데이터 정의입니다. 기존 파일은 자동 갱신하지 않으므로 시트 구조 변경 시 함께 수정하세요.
namespace HumanGhost.Data.Definitions
{
    public sealed class CorruptionStep
    {
        public int ID { get; }
        public int Step { get; }
        public int MinCorruption { get; }
        public int InkAlpha { get; }
        public CorruptionStep(int id, int step, int minCorruption, int inkAlpha)
        {
            ID = id;
            Step = step;
            MinCorruption = minCorruption;
            InkAlpha = inkAlpha;
        }
    }
}
