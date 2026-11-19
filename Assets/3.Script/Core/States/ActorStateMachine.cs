using System;
using HumanGhost.Data.Definitions;

namespace HumanGhost.Gameplay.States
{
    // 클래스 상태와 개별 행동 함수 모두 같은 전환 지점에서 종료·진입시킵니다.
    public sealed class ActorStateMachine
    {
        private Action<int> _tick;
        private Action _exit;
        private bool _transitioning;

        public StateKey Current { get; private set; }
        public event Action<StateKey> Changed;

        public ActorStateMachine(ActorState initial)
        {
            Change(initial);
        }

        public bool Change(ActorState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return Transition(state.Key, state.Enter, state.Tick, state.Exit);
        }

        // Jump 등 개별 행동은 상태 클래스를 추가하지 않고 함수를 호출합니다.
        public bool Call(StateKey key, Action enter = null, Action<int> tick = null, Action exit = null)
            => Transition(key, enter, tick, exit);

        public void Tick(int elapsedMilliseconds)
        {
            if (elapsedMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(elapsedMilliseconds));
            if (_transitioning) throw new InvalidOperationException("상태 진입·종료·변경 통지 중에는 Tick을 호출할 수 없습니다.");
            _tick?.Invoke(elapsedMilliseconds);
        }

        private bool Transition(StateKey key, Action enter, Action<int> tick, Action exit)
        {
            if (!Enum.IsDefined(typeof(StateKey), key)) throw new ArgumentOutOfRangeException(nameof(key));
            if (_transitioning) throw new InvalidOperationException("상태 진입·종료·변경 통지 중 재전환할 수 없습니다. 통지 소비자는 코어 명령을 다시 호출하지 않습니다.");
            if (Current == key) return false;

            // 완료 통지(Changed)까지 같은 보호 구간에 둡니다. 통지 소비자의 재전환이 이벤트 순서를 중첩시키지 않게 합니다(조건 규칙 감사 4.2).
            // 소비자 예외는 격리하지 않고 전파하며, 상태는 이미 확정돼 있고 보호는 finally에서 풀립니다.
            _transitioning = true;
            try
            {
                _exit?.Invoke();
                Current = key;
                _tick = tick;
                _exit = exit;
                enter?.Invoke();
                Changed?.Invoke(key);
            }
            finally { _transitioning = false; }
            return true;
        }
    }
}
