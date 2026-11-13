using System;

namespace HumanGhost.Gameplay.Adapters
{
    // 앱 설정의 선택 언어만 공유합니다. 데이터와 게임 진행은 소유하지 않습니다.
    public static class LanguageSettings
    {
        private static LanguageCode _current;
        public static LanguageCode Current => _current;
        public static event Action Changed;

        internal static void Reset(LanguageCode language)
        {
            Changed = null;
            _current = language;
        }

        public static void Apply(LanguageCode language)
        {
            if (language != LanguageCode.Korean && language != LanguageCode.English)
                throw new ArgumentOutOfRangeException(nameof(language));
            if (_current == language) return;
            _current = language;
            Changed?.Invoke();
        }
    }
}
