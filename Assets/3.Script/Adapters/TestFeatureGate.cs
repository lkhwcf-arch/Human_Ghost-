using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    // 테스트 전용 기능의 런타임 사용 여부를 한곳에서 판단합니다.
    // 개발 빌드는 항상 켜고 출시 빌드는 항상 끕니다. 에디터는 "인귀/출시 빌드처럼 보기" 설정을 따릅니다.
    // 컴파일 조건(#if UNITY_EDITOR || DEVELOPMENT_BUILD)은 그대로 두고, 에디터에서 출시 빌드와 같은 화면을 보기 위한 실행 조건만 더합니다.
    public static class TestFeatureGate
    {
#if UNITY_EDITOR
        public const string ReleasePreviewKey = "HumanGhost.ReleasePreview";
        private static bool? _releasePreview;

        // Play 중에는 설정을 바꾸지 않으므로 Play 시작 시 한 번 읽은 값을 사용합니다.
        public static bool IsReleasePreview
        {
            get
            {
                if (!_releasePreview.HasValue) _releasePreview = UnityEditor.EditorPrefs.GetBool(ReleasePreviewKey, false);
                return _releasePreview.Value;
            }
        }

        public static void SetReleasePreview(bool value)
        {
            if (Application.isPlaying) throw new System.InvalidOperationException("Play 중에는 출시 빌드 미리보기 설정을 바꿀 수 없습니다.");
            UnityEditor.EditorPrefs.SetBool(ReleasePreviewKey, value);
            _releasePreview = value;
        }

        public static bool IsEnabled => !IsReleasePreview;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => _releasePreview = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AnnounceReleasePreview()
        {
            if (IsReleasePreview)
                Debug.LogWarning("출시 빌드 미리보기 모드입니다. 테스트 기능(F8·F9·H·수동 저장·판정 표시·테스트 메뉴 등)을 끄고 출시 저장 모드로 실행합니다. 해제: 인귀/출시 빌드처럼 보기");
        }
#elif DEVELOPMENT_BUILD
        // const로 두면 사용처에 도달 불가 코드 경고가 생기므로 프로퍼티로 고정값을 반환합니다.
        public static bool IsReleasePreview => false;
        public static bool IsEnabled => true;
#else
        public static bool IsReleasePreview => true;
        public static bool IsEnabled => false;
#endif
    }
}
