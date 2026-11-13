using System;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Storage;

namespace HumanGhost.Gameplay.Adapters
{
    // 언어별 열 선택은 한 곳에서 처리하고 화면에는 문구 ID와 언어 코드만 받습니다.
    public class LanguageTextSource
    {
        private readonly IReadOnlyDataTable<Language> _language;

        public LanguageTextSource(IReadOnlyDataCatalog data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            _language = data.GetTable<Language>();
        }

        public virtual string Read(int textId, LanguageCode languageCode)
        {
            return Select(_language.Get(textId), languageCode);
        }

        public string Read(int textId) => Read(textId, LanguageSettings.Current);
        public static string Select(Language row) => Select(row, LanguageSettings.Current);
        public static string Select(Language row, LanguageCode languageCode)
        {
            switch (languageCode)
            {
                case LanguageCode.Korean: return row.KO;
                case LanguageCode.English: return row.EN;
                default: throw new ArgumentOutOfRangeException(nameof(languageCode), "지원하지 않는 언어 코드입니다.");
            }
        }
    }
}
