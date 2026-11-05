using System;
using HumanGhost.Gameplay.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HumanGhost.Gameplay.Adapters
{
    // 공유 입력 에셋을 변경하지 않습니다. 컷씬이 진행 중일 때만 선택한 명령을 전달합니다.
    public class CutsceneInput : MonoBehaviour
    {
        [SerializeField, InspectorName("컷씬 화면")] private CutsceneScreen _screen = null;
        [SerializeField, InspectorName("다음 장 입력")] private InputActionReference _advance = null;
        [SerializeField, InspectorName("건너뛰기 유지 입력")] private InputActionReference _skip = null;
        private InputAction _ownedAdvance, _ownedSkip;

        protected virtual void OnEnable()
        {
            if (_screen == null || _advance == null || _skip == null || _advance.action == null || _skip.action == null)
                throw new InvalidOperationException("컷씬 화면·다음 장·건너뛰기 입력을 연결해 주세요.");
            try
            {
                _ownedAdvance = _advance.action.Clone();
                _ownedSkip = _skip.action.Clone();
                _ownedAdvance.performed += OnAdvance;
                _ownedSkip.performed += OnSkip;
                _ownedSkip.canceled += OnSkip;
                _ownedAdvance.Enable();
                _ownedSkip.Enable();
            }
            catch { Release(); throw; }
        }

        private void OnAdvance(InputAction.CallbackContext context)
        {
            if (_screen != null && _screen.IsRunning) _screen.Advance();
        }
        private void OnSkip(InputAction.CallbackContext context)
        {
            if (_screen != null && _screen.IsRunning) _screen.SetSkipHeld(context.ReadValueAsButton());
        }
        protected virtual void OnDisable() => Release();
        private void Release()
        {
            if (_screen != null) _screen.SetSkipHeld(false);
            if (_ownedAdvance != null)
            {
                _ownedAdvance.performed -= OnAdvance;
                _ownedAdvance.Dispose();
                _ownedAdvance = null;
            }
            if (_ownedSkip != null)
            {
                _ownedSkip.performed -= OnSkip;
                _ownedSkip.canceled -= OnSkip;
                _ownedSkip.Dispose();
                _ownedSkip = null;
            }
        }
    }
}
