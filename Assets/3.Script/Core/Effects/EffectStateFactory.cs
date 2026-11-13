using System;
using HumanGhost.Data.Definitions;

namespace HumanGhost.Gameplay.Effects
{
    // 공통 생성 절차를 부모에 두고 실제 상태 생성만 자식 팩토리 메서드가 재정의합니다.
    public abstract class EffectStateFactory
    {
        public abstract EffectExecution Execution { get; }
        public EffectState Create(int sourceID, EffectDefinition definition, EffectTarget target,
            IEffectAction action, Action<EffectResult> publish)
        {
            if (definition == null || target == null || action == null) throw new ArgumentNullException(nameof(definition));
            if (definition.Execution != Execution) throw new InvalidOperationException("효과 실행 방식과 생성 담당이 다릅니다.");
            if (Execution == EffectExecution.Periodic && definition.IntervalMs <= 0)
                throw new ArgumentException("주기 실행에는 양의 주기가 필요합니다.", nameof(definition));
            return CreateState(sourceID, definition, target, action, publish);
        }
        protected abstract EffectState CreateState(int sourceID, EffectDefinition definition, EffectTarget target,
            IEffectAction action, Action<EffectResult> publish);
    }

    public class ImmediateEffectStateFactory : EffectStateFactory
    {
        public override EffectExecution Execution => EffectExecution.Immediate;
        protected override EffectState CreateState(int sourceID, EffectDefinition definition, EffectTarget target, IEffectAction action, Action<EffectResult> publish)
            => new ImmediateEffectState(sourceID, definition, target, action, publish);
    }
    public class PeriodicEffectStateFactory : EffectStateFactory
    {
        public override EffectExecution Execution => EffectExecution.Periodic;
        protected override EffectState CreateState(int sourceID, EffectDefinition definition, EffectTarget target, IEffectAction action, Action<EffectResult> publish)
            => new PeriodicEffectState(sourceID, definition, target, action, publish);
    }
    public class MaintainedEffectStateFactory : EffectStateFactory
    {
        public override EffectExecution Execution => EffectExecution.Maintained;
        protected override EffectState CreateState(int sourceID, EffectDefinition definition, EffectTarget target, IEffectAction action, Action<EffectResult> publish)
            => new MaintainedEffectState(sourceID, definition, target, action, publish);
    }
}
