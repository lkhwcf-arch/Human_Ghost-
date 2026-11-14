using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HumanGhost.Gameplay.Adapters
{
    // 메뉴마다 입력 에셋 복제·구독·해제를 반복하지 않도록 한곳에서 소유합니다.
    public sealed class MenuInput : IDisposable
    {
        private readonly InputActionAsset _owned;
        private readonly InputAction _navigate, _submit, _cancel;
        private readonly Action<int> _move;
        private readonly Action _accept, _back;
        private readonly Action<int> _side;
        private readonly bool _acceptBothJumpKeys;
        private int _horizontal;
        private bool _enabled;
        private bool _blocked;
        private bool _requested;
        private int _vertical;

        // 메뉴가 "뒤로"(키보드 ESC·패드 B)로 무언가를 닫은 마지막 프레임입니다.
        // 같은 프레임의 ESC로 일시정지가 다시 열리지 않도록 일시정지 담당이 확인합니다.
        public static int LastBackFrame { get; private set; } = -1;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetLastBackFrame() => LastBackFrame = -1;

        // side는 좌우 값 바꾸기(선택)입니다.
        public MenuInput(InputActionAsset source, Action<int> move, Action accept, Action back, Action<int> side = null, bool acceptBothJumpKeys = false)
        {
            _side = side;
            _acceptBothJumpKeys = acceptBothJumpKeys;
            if (source == null) throw new ArgumentNullException(nameof(source));
            _move = move ?? throw new ArgumentNullException(nameof(move));
            _accept = accept ?? throw new ArgumentNullException(nameof(accept));
            _back = back ?? throw new ArgumentNullException(nameof(back));
            _owned = UnityEngine.Object.Instantiate(source);
            try
            {
                _owned.Disable();
                _navigate = _owned.FindAction("UI/Navigate", true);
                _submit = _owned.FindAction("UI/Submit", true);
                _submit.AddBinding("<Keyboard>/c");
                if (_acceptBothJumpKeys) _submit.AddBinding("<Keyboard>/k");
                _cancel = _owned.FindAction("UI/Cancel", true);
            }
            catch { UnityEngine.Object.Destroy(_owned); throw; }
        }

        public void Enable()
        {
            _requested = true;
            if (_blocked) return;
            if (_enabled) return;
            // 비활성 중 조작 방식이 바뀌었어도 다시 켤 때 현재 설정을 사용합니다.
            ApplyKeyboardLayout();
            _enabled = true;
            KeyboardControls.Changed += OnKeyboardChanged;
            _navigate.performed += OnNavigate;
            _navigate.canceled += OnNavigate;
            _submit.performed += OnSubmit;
            _cancel.performed += OnCancel;
            _navigate.Enable(); _submit.Enable(); _cancel.Enable();
        }

        public void Disable()
        {
            _requested = false;
            if (!_enabled) return;
            _enabled = false;
            KeyboardControls.Changed -= OnKeyboardChanged;
            _navigate.performed -= OnNavigate;
            _navigate.canceled -= OnNavigate;
            _submit.performed -= OnSubmit;
            _cancel.performed -= OnCancel;
            _owned.Disable();
            _vertical = 0;
            _horizontal = 0;
        }

        public void SetBlocked(bool blocked)
        {
            bool requested = _requested;
            _blocked = blocked;
            if (blocked) Disable();
            _requested = requested;
            if (!blocked && requested) Enable();
        }

        private void OnNavigate(InputAction.CallbackContext context)
        {
            KeyboardControls.NoteDevice(context.control.device);
            float side = context.ReadValue<Vector2>().x;
            int sideDirection = side > 0.5f ? 1 : side < -0.5f ? -1 : 0;
            if (_side != null && sideDirection != 0 && sideDirection != _horizontal) _side(sideDirection);
            _horizontal = sideDirection;
            float value = context.ReadValue<Vector2>().y;
            int direction = value > 0.5f ? -1 : value < -0.5f ? 1 : 0;
            if (direction != 0 && direction != _vertical) _move(direction);
            _vertical = direction;
        }
        private void ApplyKeyboardLayout()
        {
            KeyboardControls.Apply(_owned);
            if (!_acceptBothJumpKeys) return;
            for (int i = 0; i < _submit.bindings.Count; i++)
                if (_submit.bindings[i].path == "<Keyboard>/c" || _submit.bindings[i].path == "<Keyboard>/k")
                    _submit.RemoveBindingOverride(i);
        }
        private void OnKeyboardChanged()
        {
            bool requested = _requested;
            Disable();
            if (requested) Enable();
        }
        private void OnSubmit(InputAction.CallbackContext context)
        {
            KeyboardControls.NoteDevice(context.control.device);
            _accept();
        }
        private void OnCancel(InputAction.CallbackContext context)
        {
            LastBackFrame = Time.frameCount;
            _back();
        }
        public void Dispose() { Disable(); if (_owned != null) UnityEngine.Object.Destroy(_owned); }
    }
}
