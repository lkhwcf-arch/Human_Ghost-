using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HumanGhost.Gameplay.Adapters
{
    // 소유자가 명시한 대상만 호출합니다. 매 프레임 키 검사와 길게 누르기를 사용하지 않습니다.
    public sealed class SkipKeyInput : IDisposable
    {
        private readonly Func<bool> _canSkip;
        private readonly Action _skip;
        private InputAction _action;
        private readonly int _connectedFrame;
        private static int _consumedFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetConsumption() => _consumedFrame = -1;

        public SkipKeyInput(Func<bool> canSkip, Action skip)
        {
            _canSkip = canSkip ?? throw new ArgumentNullException(nameof(canSkip));
            _skip = skip ?? throw new ArgumentNullException(nameof(skip));
            _connectedFrame = Time.frameCount;
            _action = new InputAction("Skip", InputActionType.Button, "<Keyboard>/enter", interactions: "Press(behavior=0)");
            _action.AddBinding("<Keyboard>/numpadEnter");
            _action.performed += OnPressed;
            try { _action.Enable(); }
            catch { Dispose(); throw; }
        }

        private void OnPressed(InputAction.CallbackContext context)
        {
            if (_action == null || Time.frameCount == _connectedFrame || _consumedFrame == Time.frameCount || !_canSkip()) return;
            // 실행 중 새 화면이 연결돼도 같은 입력을 다시 전달하지 않습니다.
            _consumedFrame = Time.frameCount;
            _skip();
        }

        public void Dispose()
        {
            InputAction action = _action;
            _action = null;
            if (action == null) return;
            action.performed -= OnPressed;
            action.Dispose();
        }
    }
}
