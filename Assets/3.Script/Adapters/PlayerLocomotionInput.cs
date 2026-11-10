using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HumanGhost.Gameplay.Adapters
{
    // 입력 에셋을 개체별 복제해 활성 수명과 구독을 독립적으로 소유합니다.
    public sealed class PlayerLocomotionInput : MonoBehaviour, IModalInputTarget
    {
        [SerializeField] private InputActionReference _move = null;
        [SerializeField] private InputActionReference _jump = null;
        [SerializeField] private InputActionReference _attack = null;
        [SerializeField] private InputActionReference _roll = null;
        [SerializeField] private InputActionReference _interact = null;
        [SerializeField] private InputActionReference _skill = null;
        [SerializeField] private InputActionReference _herb = null;
        private InputActionAsset _ownedAsset;
        internal InputActionAsset Source => _move?.action?.actionMap?.asset;
        private InputAction _moveAction, _jumpAction, _attackAction, _rollAction, _interactAction, _skillAction, _herbAction;
        private PlayerParryInput _parryInput;
        private int _vertical;
        public int VerticalDirection => _vertical;
        private PlayerLocomotion _locomotion;
        private PlayerActor _actor;
        private bool _subscribed;
        public event Action JumpAccepted;
        public Func<bool> TryDropThrough { private get; set; }
        // 코어가 점프를 받지 않은 입력입니다. 점프 선입력·매달리기 올라가기에 씁니다.
        public event Action JumpRejected;
        public event Action InteractPressed;
        // 위(1)·아래(-1)를 새로 누른 순간입니다.
        public event Action<int> VerticalPressed;
        // 잠긴 동안의 공격 입력입니다. 텍스트박스 줄 넘김에 쓰고 공격은 나가지 않습니다.
        public event Action AdvancePressed;
        private bool _locked;
        private bool _modalBlocked;
        private int _unlockFrame = -1;
        private bool IsBlocked => _locked || _modalBlocked || Time.frameCount <= _unlockFrame;

        public void SetModalBlocked(bool blocked)
        {
            if (this == null || _modalBlocked == blocked) return;
            _modalBlocked = blocked;
            DiscardBufferedInput();
        }

        // 대화/일시정지 잠금과 오류 모달 잠금은 독립적으로 유지합니다.
        public void SetLocked(bool locked)
        {
            if (this == null || _locked == locked) return;
            _locked = locked;
            DiscardBufferedInput();
        }

        public void DiscardBufferedInput()
        {
            _vertical = 0;
            _unlockFrame = Time.frameCount;
            _actor?.DiscardBufferedInput();
            if (_locomotion != null) SetDirection(0);
        }

        private void Update()
        {
            if (_locked || _modalBlocked || _unlockFrame < 0 || Time.frameCount <= _unlockFrame) return;
            _unlockFrame = -1;
            Vector2 value = _subscribed ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
            SetDirection(Math.Sign(value.x));
            _vertical = value.y > 0.5f ? 1 : value.y < -0.5f ? -1 : 0;
        }

        public void Initialize(PlayerLocomotion locomotion) => InitializeCore(locomotion, null);
        public void Initialize(PlayerActor actor)
        {
            if (actor == null) throw new ArgumentNullException(nameof(actor));
            InitializeCore(actor.Locomotion, actor);
        }

        private void InitializeCore(PlayerLocomotion locomotion, PlayerActor actor)
        {
            if (locomotion == null) throw new ArgumentNullException(nameof(locomotion));
            if (_locomotion != null) throw new InvalidOperationException("이미 초기화된 입력 어댑터입니다.");
            if (_move == null || _jump == null || _move.action == null || _jump.action == null || _move.action == _jump.action ||
                _move.action.type != InputActionType.Value || _move.action.expectedControlType != "Vector2" || _jump.action.type != InputActionType.Button)
                throw new InvalidOperationException("Move(Vector2 Value)·Jump(Button) 참조를 확인하세요.");
            InputActionAsset source = _move.action.actionMap.asset;
            if (source == null || _jump.action.actionMap.asset != source ||
                (_attack != null && (_attack.action == null || _attack.action.actionMap.asset != source || _attack.action.type != InputActionType.Button)) ||
                (_roll != null && (_roll.action == null || _roll.action.actionMap.asset != source || _roll.action.type != InputActionType.Button)) ||
                (_interact != null && (_interact.action == null || _interact.action.actionMap.asset != source || _interact.action.type != InputActionType.Button)) ||
                (_skill != null && (_skill.action == null || _skill.action.actionMap.asset != source || _skill.action.type != InputActionType.Button)) ||
                (_herb != null && (_herb.action == null || _herb.action.actionMap.asset != source || _herb.action.type != InputActionType.Button)))
                throw new InvalidOperationException("입력 참조는 동일 에셋의 올바른 Action이어야 합니다.");
            _ownedAsset = Instantiate(source);
            _ownedAsset.Disable();
            _moveAction = _ownedAsset.FindAction(_move.action.id.ToString(), true);
            _jumpAction = _ownedAsset.FindAction(_jump.action.id.ToString(), true);
            _attackAction = _attack == null ? null : _ownedAsset.FindAction(_attack.action.id.ToString(), true);
            _rollAction = _roll == null ? null : _ownedAsset.FindAction(_roll.action.id.ToString(), true);
            _interactAction = _interact == null ? null : _ownedAsset.FindAction(_interact.action.id.ToString(), true);
            _skillAction = _skill == null ? null : _ownedAsset.FindAction(_skill.action.id.ToString(), true);
            _herbAction = _herb == null ? null : _ownedAsset.FindAction(_herb.action.id.ToString(), true);
            _moveAction.AddBinding("<Gamepad>/leftStick");
            _moveAction.AddBinding("<Gamepad>/dpad");
            _jumpAction.AddBinding("<Gamepad>/buttonSouth");
            if (_attackAction != null) _attackAction.AddBinding("<Gamepad>/buttonWest");
            if (_rollAction != null) _rollAction.AddBinding("<Gamepad>/buttonEast");
            KeyboardControls.Apply(_ownedAsset);
            _locomotion = locomotion; _actor = actor;
            if (actor != null) _parryInput = new PlayerParryInput(actor, () => IsBlocked);
            if (isActiveAndEnabled) Subscribe();
        }

        private void OnEnable() { if (_locomotion != null) Subscribe(); }

        private void OnDisable()
        {
            if (!_subscribed) return;
            KeyboardControls.Changed -= OnKeyboardChanged;
            _moveAction.performed -= OnMove; _moveAction.canceled -= OnMove;
            _jumpAction.performed -= OnJump;
            if (_attackAction != null) _attackAction.performed -= OnAttack;
            if (_rollAction != null) _rollAction.performed -= OnRoll;
            if (_interactAction != null) _interactAction.started -= OnInteract;
            if (_skillAction != null) _skillAction.performed -= OnSkill;
            if (_herbAction != null) _herbAction.performed -= OnHerb;
            _parryInput?.Disable();
            _subscribed = false;
            _vertical = 0;
            _ownedAsset.Disable();
            _actor?.DiscardBufferedInput();
            if (_actor != null) _actor.SetMove(0); else _locomotion.SetMove(0);
        }

        private void OnDestroy()
        {
            OnDisable();
            _parryInput?.Dispose();
            if (_ownedAsset == null) return;
            if (Application.isPlaying) Destroy(_ownedAsset); else DestroyImmediate(_ownedAsset);
            _ownedAsset = null;
        }

        private void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;
            KeyboardControls.Changed += OnKeyboardChanged;
            _moveAction.performed += OnMove; _moveAction.canceled += OnMove;
            _jumpAction.performed += OnJump;
            if (_actor != null)
            {
                _parryInput.Enable();
                if (_attackAction != null) { _attackAction.performed += OnAttack; _attackAction.Enable(); }
                if (_rollAction != null) { _rollAction.performed += OnRoll; _rollAction.Enable(); }
                // Interact 액션은 Hold라 누른 순간(started)에 받습니다.
                if (_interactAction != null) { _interactAction.started += OnInteract; _interactAction.Enable(); }
                if (_skillAction != null) { _skillAction.performed += OnSkill; _skillAction.Enable(); }
                if (_herbAction != null) { _herbAction.performed += OnHerb; _herbAction.Enable(); }
            }
            _moveAction.Enable(); _jumpAction.Enable();
            SetDirection(IsBlocked ? 0 : Math.Sign(_moveAction.ReadValue<Vector2>().x));
        }

        private void OnKeyboardChanged()
        {
            bool active = _subscribed;
            if (active) OnDisable();
            KeyboardControls.Apply(_ownedAsset);
            DiscardBufferedInput();
            if (active) Subscribe();
        }

        private void SetDirection(int direction)
        {
            if (_actor != null) _actor.SetMove(direction); else _locomotion.SetMove(direction);
        }
        private void OnMove(InputAction.CallbackContext context)
        {
            if (IsBlocked) return;
            KeyboardControls.NoteDevice(context.control.device);
            Vector2 value = context.canceled ? Vector2.zero : context.ReadValue<Vector2>();
            SetDirection(Math.Sign(value.x));
            int vertical = value.y > 0.5f ? 1 : value.y < -0.5f ? -1 : 0;
            if (vertical != 0 && vertical != _vertical) VerticalPressed?.Invoke(vertical);
            _vertical = vertical;
        }
        private void OnInteract(InputAction.CallbackContext context) { if (!IsBlocked) InteractPressed?.Invoke(); }
        private void OnSkill(InputAction.CallbackContext context) { if (!IsBlocked) _actor.TryUseSkill(); }
        private void OnHerb(InputAction.CallbackContext context) { if (!IsBlocked) _actor.TryUseHerb(); }
        private void OnJump(InputAction.CallbackContext context)
        {
            if (IsBlocked) return;
            KeyboardControls.NoteDevice(context.control.device);
            if (_moveAction.ReadValue<Vector2>().y < -0.5f && TryDropThrough != null && TryDropThrough()) return;
            if (_actor != null ? _actor.TryJump() : _locomotion.TryJump()) JumpAccepted?.Invoke();
            else JumpRejected?.Invoke();
        }
        private void OnAttack(InputAction.CallbackContext context) { if (_modalBlocked || Time.frameCount <= _unlockFrame) return; if (_locked) AdvancePressed?.Invoke(); else _actor.TryAttack(); }
        private void OnRoll(InputAction.CallbackContext context) { if (!IsBlocked) _actor.TryRoll(); }
    }
}
