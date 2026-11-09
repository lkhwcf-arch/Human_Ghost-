using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    // 개인 설정을 화면에 적용하는 엔진 경계입니다. 매 프레임 확인하지 않습니다.
    public static class WindowModeApplication
    {
        private static WindowMode? _requestedMode;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => _requestedMode = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize() => Apply(GameSettingsStore.Load().WindowMode);

        public static void Apply(WindowMode mode)
        {
            // 편집기 창 크기는 바꾸지 않고 실행파일에서만 실제 화면 모드를 적용합니다.
            if (Application.isEditor || _requestedMode == mode) return;
            if (mode == WindowMode.Windowed)
            {
                RectInt area = Screen.mainWindowDisplayInfo.workArea;
                int availableWidth = area.width > 0 ? area.width : Screen.currentResolution.width;
                int availableHeight = area.height > 0 ? area.height : Screen.currentResolution.height;
                // 작업 표시줄을 제외한 영역에서 창 테두리 여유를 남기고 16:9를 유지합니다.
                int units = System.Math.Max(1, System.Math.Min(100,
                    System.Math.Min((availableWidth - 32) / 16, (availableHeight - 64) / 9)));
                Screen.SetResolution(units * 16, units * 9, FullScreenMode.Windowed);
            }
            else Screen.SetResolution(1920, 1080, FullScreenMode.FullScreenWindow);
            // 화면 전환은 프레임 끝에 반영되므로 같은 요청을 중복 발행하지 않습니다.
            _requestedMode = mode;
        }
    }
}
