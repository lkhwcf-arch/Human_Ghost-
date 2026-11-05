// 최초 생성 데이터 정의입니다. 기존 파일은 자동 갱신하지 않으므로 시트 구조 변경 시 함께 수정하세요.
namespace HumanGhost.Data.Definitions
{
    public sealed class Sound
    {
        public int ID { get; }
        public string Address { get; }
        public int Category { get; }
        public int Volume { get; }
        public bool Loop { get; }
        public int FadeInMs { get; }
        public int FadeOutMs { get; }
        public Sound(int id, string address, int category, int volume, bool loop, int fadeInMs, int fadeOutMs)
        {
            ID = id;
            Address = address;
            Category = category;
            Volume = volume;
            Loop = loop;
            FadeInMs = fadeInMs;
            FadeOutMs = fadeOutMs;
        }
    }
}
