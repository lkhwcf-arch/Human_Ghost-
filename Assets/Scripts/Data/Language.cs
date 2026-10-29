// 자동 생성 데이터 정의입니다. 직접 수정하면 다음 생성 시 덮어씁니다.
namespace HumanGhost.Data.Definitions
{
    public sealed class Language
    {
        public int ID { get; }
        public string KO { get; }
        public string EN { get; }
        public Language(
            int id,
            string ko,
            string en
        )
        {
            ID = id;
            KO = ko;
            EN = en;
        }
    }
}
