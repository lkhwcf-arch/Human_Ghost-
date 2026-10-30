using System;
using HumanGhost.Data.Definitions;

namespace HumanGhost.Gameplay.States
{
    // 공통 상태의 수명 주기. 유닛별 실제 동작은 호출자가 전달합니다.
    public abstract class ActorState
    {
        private readonly Action _enter;
        private readonly Action<int> _tick;
        private readonly Action _exit;

        public StateKey Key { get; }

        protected ActorState(StateKey key, Action enter, Action<int> tick, Action exit)
        {
            Key = key;
            _enter = enter;
            _tick = tick;
            _exit = exit;
        }

        public virtual void Enter() => _enter?.Invoke();
        public virtual void Tick(int elapsedMilliseconds) => _tick?.Invoke(elapsedMilliseconds);
        public virtual void Exit() => _exit?.Invoke();
    }

    public sealed class IdleState : ActorState
    {
        public IdleState(Action enter = null, Action<int> tick = null, Action exit = null)
            : base(StateKey.Idle, enter, tick, exit) { }
    }

    public sealed class WalkState : ActorState
    {
        public WalkState(Action enter = null, Action<int> tick = null, Action exit = null)
            : base(StateKey.Walk, enter, tick, exit) { }
    }
}
