// 자동 생성 파일입니다. 원본 시트를 수정한 뒤 다시 배포하세요.
namespace HumanGhost.Data.Definitions
{
    public sealed class Language
    {
        public int ID { get; }
        public string KO { get; }
        public string EN { get; }
        public Language(int id, string ko, string en)
        {
            ID = id;
            KO = ko;
            EN = en;
        }
    }
}
