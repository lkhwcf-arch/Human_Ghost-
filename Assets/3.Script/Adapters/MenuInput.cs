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
        private bool _enabled;
        private int _vertical;

        public MenuInput(InputActionAsset source, Action<int> move, Action accept, Action back)
        {
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
                _cancel = _owned.FindAction("UI/Cancel", true);
            }
            catch { UnityEngine.Object.Destroy(_owned); throw; }
        }

        public void Enable()
        {
            if (_enabled) return;
            _enabled = true;
            _navigate.performed += OnNavigate;
            _navigate.canceled += OnNavigate;
            _submit.performed += OnSubmit;
            _cancel.performed += OnCancel;
            _navigate.Enable(); _submit.Enable(); _cancel.Enable();
        }

        public void Disable()
        {
            if (!_enabled) return;
            _enabled = false;
            _navigate.performed -= OnNavigate;
            _navigate.canceled -= OnNavigate;
            _submit.performed -= OnSubmit;
            _cancel.performed -= OnCancel;
            _owned.Disable();
            _vertical = 0;
        }

        private void OnNavigate(InputAction.CallbackContext context)
        {
            float value = context.ReadValue<Vector2>().y;
            int direction = value > 0.5f ? -1 : value < -0.5f ? 1 : 0;
            if (direction != 0 && direction != _vertical) _move(direction);
            _vertical = direction;
        }
        private void OnSubmit(InputAction.CallbackContext context) => _accept();
        private void OnCancel(InputAction.CallbackContext context) => _back();
        public void Dispose() { Disable(); if (_owned != null) UnityEngine.Object.Destroy(_owned); }
    }
}
