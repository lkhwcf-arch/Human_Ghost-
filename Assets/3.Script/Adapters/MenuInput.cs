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
        private int _horizontal;
        private bool _enabled;
        private bool _blocked;
        private bool _requested;
        private int _vertical;

        // side는 좌우 값 바꾸기(선택)입니다.
        public MenuInput(InputActionAsset source, Action<int> move, Action accept, Action back, Action<int> side = null)
        {
            _side = side;
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
                KeyboardControls.Apply(_owned);
                _cancel = _owned.FindAction("UI/Cancel", true);
            }
            catch { UnityEngine.Object.Destroy(_owned); throw; }
        }

        public void Enable()
        {
            _requested = true;
            if (_blocked) return;
            if (_enabled) return;
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
        private void OnKeyboardChanged()
        {
            bool requested = _requested;
            Disable();
            KeyboardControls.Apply(_owned);
            if (requested) Enable();
        }
        private void OnSubmit(InputAction.CallbackContext context)
        {
            KeyboardControls.NoteDevice(context.control.device);
            _accept();
        }
        private void OnCancel(InputAction.CallbackContext context) => _back();
        public void Dispose() { Disable(); if (_owned != null) UnityEngine.Object.Destroy(_owned); }
    }
}
