using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HumanGhost.Gameplay.Adapters
{
    // 이동 입력 에셋과 독립적인 패링 입력입니다. 조립자가 Actor와 잠금 조건을 전달합니다.
    public sealed class PlayerParryInput : IDisposable
    {
        private readonly PlayerActor _actor;
        private readonly Func<bool> _isBlocked;
        private readonly InputAction _action;
        private int _unlockFrame = -1;
        private bool _enabled, _disposed;

        public PlayerParryInput(PlayerActor actor, Func<bool> isBlocked)
        {
            _actor = actor ?? throw new ArgumentNullException(nameof(actor));
            _isBlocked = isBlocked ?? throw new ArgumentNullException(nameof(isBlocked));
            _action = new InputAction("Parry", InputActionType.Button, "<Keyboard>/leftShift");
            _action.AddBinding("<Keyboard>/rightShift"); _action.AddBinding("<Gamepad>/leftTrigger");
            ApplyKeyboardLayout();
        }

        public void Enable()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PlayerParryInput));
            if (_enabled) return;
            ApplyKeyboardLayout(); _enabled = true;
            KeyboardControls.Changed += OnKeyboardChanged;
            _action.performed += OnParry; _action.Enable();
        }
        public void Disable()
        {
            if (!_enabled) return;
            _enabled = false;
            KeyboardControls.Changed -= OnKeyboardChanged;
            _action.performed -= OnParry; _action.Disable();
        }
        private void OnKeyboardChanged()
        {
            _action.Disable(); ApplyKeyboardLayout();
            _unlockFrame = Time.frameCount;
            if (_enabled) _action.Enable();
        }
        private void ApplyKeyboardLayout()
        {
            bool wasd = KeyboardControls.Layout == KeyboardLayout.Wasd;
            _action.ApplyBindingOverride(0, wasd ? "" : "<Keyboard>/leftShift");
            _action.ApplyBindingOverride(1, wasd ? "<Keyboard>/rightShift" : "");
        }
        private void OnParry(InputAction.CallbackContext context)
        {
            if (!_enabled || _isBlocked() || Time.frameCount <= _unlockFrame) return;
            KeyboardControls.NoteDevice(context.control == null ? null : context.control.device);
            _actor.TryParry();
        }
        public void Dispose()
        {
            if (_disposed) return;
            Disable(); _action.Dispose(); _disposed = true;
        }
    }
}
