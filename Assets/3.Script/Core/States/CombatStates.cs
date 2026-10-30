using System;
using HumanGhost.Data.Definitions;

namespace HumanGhost.Gameplay.States
{
    public sealed class AttackState : ActorState
    {
        public AttackState(StateKey key) : base(key, null, null, null)
        {
            if (key != StateKey.Attack1 && key != StateKey.Attack2 && key != StateKey.Attack3)
                throw new ArgumentOutOfRangeException(nameof(key));
        }
    }

    public sealed class HitState : ActorState
    {
        public HitState(bool critical) : base(critical ? StateKey.CritHit : StateKey.Hit, null, null, null) { }
    }

    public sealed class DeadState : ActorState
    {
        public DeadState() : base(StateKey.Dead, null, null, null) { }
    }

    public sealed class ExecuteState : ActorState
    {
        public ExecuteState() : base(StateKey.Execute, null, null, null) { }
    }
}
