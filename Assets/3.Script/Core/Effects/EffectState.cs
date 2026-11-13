using System;
using HumanGhost.Data.Definitions;

namespace HumanGhost.Gameplay.Effects
{
    public abstract class EffectState : IDisposable
    {
        private readonly IEffectAction _action;
        private readonly Action<EffectResult> _publish;
        private readonly int _sourceID;
        private bool _started;
        private bool _ended;
        private bool _applying;
        private bool _endPending;
        protected EffectDefinition Definition { get; }
        protected EffectTarget Target { get; }
        public bool IsEnded => _ended;
        public virtual bool NeedsTick => false;

        protected EffectState(int sourceID, EffectDefinition definition, EffectTarget target,
            IEffectAction action, Action<EffectResult> publish)
        { _sourceID = sourceID; Definition = definition; Target = target; _action = action; _publish = publish; }

        public void Start()
        {
            if (_started || _ended) return;
            _started = true;
            Target.Released += Dispose;
            if (!Target.IsAvailable) { Dispose(); return; }
            Publish(EffectPhase.Started);
            if (!_ended) Enter();
        }
        protected abstract void Enter();
        public virtual void Tick(int elapsedMs) { }
        protected void Apply()
        {
            if (_ended) return;
            if (!Target.IsAvailable) { Dispose(); return; }
            _applying = true;
            try
            {
                bool applied = _action.Apply(Target, Definition, this);
                if (applied) Publish(EffectPhase.Applied);
            }
            finally
            {
                _applying = false;
                if (_endPending) { _endPending = false; Publish(EffectPhase.Ended); }
            }
            if (!_ended && !Target.IsAvailable) Dispose();
        }
        private void Publish(EffectPhase phase) => _publish?.Invoke(new EffectResult(_sourceID, Definition.ID, Target, phase));
        public void Dispose()
        {
            if (_ended) return;
            _ended = true;
            Target.Released -= Dispose;
            try { _action.Remove(Target, this); }
            finally
            {
                if (_started)
                {
                    if (_applying) _endPending = true;
                    else Publish(EffectPhase.Ended);
                }
            }
        }
    }

    public class ImmediateEffectState : EffectState
    {
        public ImmediateEffectState(int sourceID, EffectDefinition definition, EffectTarget target, IEffectAction action, Action<EffectResult> publish)
            : base(sourceID, definition, target, action, publish) { }
        protected override void Enter() { try { Apply(); } finally { Dispose(); } }
    }

    public class MaintainedEffectState : EffectState
    {
        public MaintainedEffectState(int sourceID, EffectDefinition definition, EffectTarget target, IEffectAction action, Action<EffectResult> publish)
            : base(sourceID, definition, target, action, publish) { }
        protected override void Enter() => Apply();
    }

    public class PeriodicEffectState : EffectState
    {
        private long _remaining;
        public override bool NeedsTick => !IsEnded;
        public PeriodicEffectState(int sourceID, EffectDefinition definition, EffectTarget target, IEffectAction action, Action<EffectResult> publish)
            : base(sourceID, definition, target, action, publish) => _remaining = definition.IntervalMs;
        protected override void Enter() { if (Definition.ApplyOnEnter) Apply(); }
        public override void Tick(int elapsedMs)
        {
            if (elapsedMs < 0) throw new ArgumentOutOfRangeException(nameof(elapsedMs));
            if (IsEnded) return;
            if (!Target.IsAvailable) { Dispose(); return; }
            _remaining -= elapsedMs;
            while (_remaining <= 0 && !IsEnded)
            {
                // 다음 예약을 먼저 확정해 알림 실패로 같은 피해가 다시 적용되지 않게 합니다.
                _remaining += Definition.IntervalMs;
                Apply();
            }
        }
    }
}
