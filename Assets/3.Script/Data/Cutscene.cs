// 최초 생성 데이터 정의입니다. 기존 파일은 자동 갱신하지 않으므로 시트 구조 변경 시 함께 수정하세요.
namespace HumanGhost.Data.Definitions
{
    public sealed class Cutscene
    {
        public int ID { get; }
        public string Key { get; }
        public int SkipHoldMs { get; }
        public Cutscene(int id, string key, int skipHoldMs)
        {
            ID = id;
            Key = key;
            SkipHoldMs = skipHoldMs;
        }
    }
}
