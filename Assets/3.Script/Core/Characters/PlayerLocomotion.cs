using System;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay.States;
using PlayerDefinition = HumanGhost.Data.Definitions.Player;

namespace HumanGhost.Gameplay
{
    // 위치·중력·충돌을 계산하지 않는 이동 요청 코어입니다.
    public sealed class PlayerLocomotion
    {
        private readonly IdleState _idle = new IdleState();
        private readonly WalkState _walk = new WalkState();
        private readonly ActorStateMachine _states;
        private int _direction;
        private bool _isGrounded;

        public PlayerDefinition Definition { get; }
        public StateKey CurrentState => _states.Current;
        public bool IsGrounded => _isGrounded;
        public int Direction => _direction;
        // 시트의 tile_x100/초 단위 그대로. Unity 월드 단위 변환은 어댑터 책임입니다.
        public bool CanMove => CurrentState == StateKey.Idle || CurrentState == StateKey.Walk || CurrentState == StateKey.Jump;
        public int HorizontalSpeed => CanMove ? _direction * Definition.MoveSpeed : 0;
        public int JumpHeight => Definition.JumpHeight;
        public event Action<StateKey> StateChanged
        {
            add { _states.Changed += value; }
            remove { _states.Changed -= value; }
        }

        public PlayerLocomotion(PlayerDefinition definition, bool isGrounded, ActorStateMachine states = null)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (definition.MoveSpeed < 0 || definition.JumpHeight < 0)
                throw new ArgumentException("이동 속도·점프 높이는 음수일 수 없습니다.", nameof(definition));
            _isGrounded = isGrounded;
            _states = states ?? new ActorStateMachine(_idle);
            if (!isGrounded) _states.Call(StateKey.Jump, EnterAirborne);
        }

        public void SetMove(int direction)
        {
            if (direction < -1 || direction > 1) throw new ArgumentOutOfRangeException(nameof(direction));
            _direction = direction;
            if (_isGrounded && CanMove) EnterGrounded();
        }

        // 실제 물리 관측을 받습니다. 점프를 승인한 뒤 같은 이전 관측을 재전달하지 않습니다.
        public void SetGrounded(bool isGrounded)
        {
            if (_isGrounded == isGrounded) return;
            _isGrounded = isGrounded;
            if (CanMove) Resume();
        }

        // true일 때 호출자는 JumpHeight로 점프 실행을 한 번 요청할 수 있습니다.
        public bool TryJump()
        {
            if (!_isGrounded || !CanMove) return false;
            return _states.Call(StateKey.Jump, EnterAirborne);
        }

        // 전투/경직 종료 시 현재 관측과 입력으로 복귀합니다.
        internal void Resume()
        {
            if (_isGrounded) EnterGrounded();
            else _states.Call(StateKey.Jump, EnterAirborne);
        }

        public void Tick(int elapsedMilliseconds) => _states.Tick(elapsedMilliseconds);

        private void EnterAirborne() => _isGrounded = false;
        private void EnterGrounded() => _states.Change(_direction == 0 ? (ActorState)_idle : _walk);
    }
}
