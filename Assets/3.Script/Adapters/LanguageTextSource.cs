using System;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Storage;

namespace HumanGhost.Gameplay.Adapters
{
    // 언어별 열 선택은 한 곳에서 처리하고 화면에는 문구 ID와 언어 코드만 받습니다.
    public class LanguageTextSource
    {
        private readonly IReadOnlyDataTable<Language> _language;
        private readonly IReadOnlyDataCatalog _data;

        public LanguageTextSource(IReadOnlyDataCatalog data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            _data = data;
            _language = data.GetTable<Language>();
        }

        public virtual string Read(int textId, LanguageCode languageCode)
        {
            return Select(_language.Get(textId), languageCode);
        }

        // 화면은 UI_Lang ID로 역할을 고르고 대사·NPC·Stage·Popup의 자체 TextID는 기존 Read를 사용합니다.
        public string ReadUi(int uiId, LanguageCode languageCode) => Read(_data.GetTable<UI_Lang>().Get(uiId).TextID, languageCode);
        public string ReadUi(int uiId) => ReadUi(uiId, LanguageSettings.Current);
        public static string SelectUi(IReadOnlyDataCatalog data, int uiId) => SelectUi(data, uiId, LanguageSettings.Current);
        public static string SelectUi(IReadOnlyDataCatalog data, int uiId, LanguageCode languageCode)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            return Select(data.GetTable<Language>().Get(data.GetTable<UI_Lang>().Get(uiId).TextID), languageCode);
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
