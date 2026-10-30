using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HumanGhost.Gameplay.Adapters
{
    // 입력 에셋을 개체별 복제해 활성 수명과 구독을 독립적으로 소유합니다.
    public sealed class PlayerLocomotionInput : MonoBehaviour
    {
        [SerializeField] private InputActionReference _move = null;
        [SerializeField] private InputActionReference _jump = null;
        [SerializeField] private InputActionReference _attack = null;
        [SerializeField] private InputActionReference _roll = null;
        private InputActionAsset _ownedAsset;
        private InputAction _moveAction, _jumpAction, _attackAction, _rollAction;
        private PlayerLocomotion _locomotion;
        private PlayerActor _actor;
        private bool _subscribed;
        public event Action JumpAccepted;

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
                (_roll != null && (_roll.action == null || _roll.action.actionMap.asset != source || _roll.action.type != InputActionType.Button)))
                throw new InvalidOperationException("입력 참조는 동일 에셋의 올바른 Action이어야 합니다.");
            _ownedAsset = Instantiate(source);
            _ownedAsset.Disable();
            _moveAction = _ownedAsset.FindAction(_move.action.id.ToString(), true);
            _jumpAction = _ownedAsset.FindAction(_jump.action.id.ToString(), true);
            _attackAction = _attack == null ? null : _ownedAsset.FindAction(_attack.action.id.ToString(), true);
            _rollAction = _roll == null ? null : _ownedAsset.FindAction(_roll.action.id.ToString(), true);
            _locomotion = locomotion; _actor = actor;
            if (isActiveAndEnabled) Subscribe();
        }

        private void OnEnable() { if (_locomotion != null) Subscribe(); }

        private void OnDisable()
        {
            if (!_subscribed) return;
            _moveAction.performed -= OnMove; _moveAction.canceled -= OnMove;
            _jumpAction.performed -= OnJump;
            if (_attackAction != null) _attackAction.performed -= OnAttack;
            if (_rollAction != null) _rollAction.performed -= OnRoll;
            _subscribed = false;
            _ownedAsset.Disable();
            if (_actor != null) _actor.SetMove(0); else _locomotion.SetMove(0);
        }

        private void OnDestroy()
        {
            OnDisable();
            if (_ownedAsset == null) return;
            if (Application.isPlaying) Destroy(_ownedAsset); else DestroyImmediate(_ownedAsset);
            _ownedAsset = null;
        }

        private void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;
            _moveAction.performed += OnMove; _moveAction.canceled += OnMove;
            _jumpAction.performed += OnJump;
            if (_actor != null)
            {
                if (_attackAction != null) { _attackAction.performed += OnAttack; _attackAction.Enable(); }
                if (_rollAction != null) { _rollAction.performed += OnRoll; _rollAction.Enable(); }
            }
            _moveAction.Enable(); _jumpAction.Enable();
            SetDirection(Math.Sign(_moveAction.ReadValue<Vector2>().x));
        }

        private void SetDirection(int direction)
        {
            if (_actor != null) _actor.SetMove(direction); else _locomotion.SetMove(direction);
        }
        private void OnMove(InputAction.CallbackContext context) => SetDirection(context.canceled ? 0 : Math.Sign(context.ReadValue<Vector2>().x));
        private void OnJump(InputAction.CallbackContext context)
        {
            if (_actor != null ? _actor.TryJump() : _locomotion.TryJump()) JumpAccepted?.Invoke();
        }
        private void OnAttack(InputAction.CallbackContext context) => _actor.TryAttack();
        private void OnRoll(InputAction.CallbackContext context) => _actor.TryRoll();
    }
}
