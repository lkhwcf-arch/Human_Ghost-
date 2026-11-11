using System;
using HumanGhost.Gameplay.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HumanGhost.Gameplay.Adapters
{
    // 다음 장 입력은 유지하고 Enter는 공통 입력을 통해 즉시 스킵합니다.
    public class CutsceneInput : MonoBehaviour
    {
        [SerializeField, InspectorName("컷씬 화면")] private CutsceneScreen _screen = null;
        [SerializeField, InspectorName("다음 장 입력")] private InputActionReference _advance = null;
        [SerializeField, InspectorName("Enter 스킵 범위")] private SkipScope _scope = SkipScope.Cutscene;
        private InputAction _ownedAdvance;
        private SkipKeyInput _skip;
        private bool _blocked;
        public CutsceneScreen Screen => _screen;
        public bool IsReady => _ownedAdvance != null && _skip != null;
        public void SetScope(SkipScope scope)
        {
            if (IsReady) throw new InvalidOperationException("컷씬 입력을 비활성화한 뒤 범위를 변경해 주세요.");
            _scope = scope;
        }
        public void Validate()
        {
            if (_screen == null)
                throw new InvalidOperationException("컷씬 화면을 연결해 주세요.");
            if (_scope != SkipScope.Cutscene && _scope != SkipScope.Video)
                throw new InvalidOperationException("컷씬 입력의 범위는 컷씬 전체 또는 영상 하나로 설정해 주세요.");
        }
        public void SetBlocked(bool blocked) => _blocked = blocked;
        protected virtual void OnEnable()
        {
            Validate();
            try
            {
                _ownedAdvance = _advance != null && _advance.action != null ? _advance.action.Clone()
                    : new InputAction("AdvanceCutscene", InputActionType.Button, "<Keyboard>/x");
                _ownedAdvance.performed += OnAdvance;
                _ownedAdvance.Enable();
                _skip = new SkipKeyInput(() => !_blocked && _screen != null && _screen.CanSkip(_scope), Skip);
            }
            catch { Release(); throw; }
        }
        private void OnAdvance(InputAction.CallbackContext context)
        {
            // Enter를 다음 장에도 배정했더라도 즉시 스킵과 이중 실행하지 않습니다.
            if (context.control?.device is Keyboard keyboard &&
                (context.control == keyboard.enterKey || context.control == keyboard.numpadEnterKey)) return;
            if (!_blocked && _screen != null && _screen.IsRunning) _screen.Advance();
        }
        private void Skip()
        {
            try { _screen.TrySkip(_scope); }
            catch (Exception error) { Debug.LogException(error, this); }
        }
        protected virtual void OnDisable() => Release();
        private void Release()
        {
            _skip?.Dispose(); _skip = null;
            if (_ownedAdvance == null) return;
            _ownedAdvance.performed -= OnAdvance;
            _ownedAdvance.Dispose(); _ownedAdvance = null;
        }
    }
}
