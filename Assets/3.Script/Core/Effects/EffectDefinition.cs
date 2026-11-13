using System;
using HumanGhost.Data.Definitions;

namespace HumanGhost.Gameplay.Effects
{
    // 원본 정의는 공유하고 진행 시간·대상·등록 토큰은 실행 상태에만 둡니다.
    public class EffectDefinition
    {
        public int ID { get; }
        public EffectExecution Execution { get; }
        public EffectOperation Operation { get; }
        public int Value { get; }
        public int IntervalMs { get; }
        public bool ApplyOnEnter { get; }
        public EffectCategory Category { get; }
        public EffectDefinition(int id, EffectExecution execution, EffectOperation operation, int value,
            int intervalMs = 0, bool applyOnEnter = true, EffectCategory category = EffectCategory.None)
        {
            ID = id; Execution = execution; Operation = operation;
            Value = value; IntervalMs = intervalMs; ApplyOnEnter = applyOnEnter;
            Category = category;
        }
    }

    // 엔진 객체를 담지 않으므로 대상 종료 뒤에도 완료 기록을 안전하게 소비할 수 있습니다.
    public readonly struct EffectResult
    {
        public int SourceID { get; }
        public int EffectID { get; }
        public int TargetID { get; }
        public long Generation { get; }
        public EffectPhase Phase { get; }
        public EffectResult(int sourceID, int effectID, EffectTarget target, EffectPhase phase)
        {
            SourceID = sourceID; EffectID = effectID; TargetID = target.ID;
            Generation = target.Generation; Phase = phase;
        }
    }

    // 실행 방식과 실제 동작은 별개 계약입니다. 새로운 규칙은 이 계약을 구현해 주입합니다.
    public interface IEffectAction
    {
        bool Apply(EffectTarget target, EffectDefinition definition, object token);
        void Remove(EffectTarget target, object token);
    }

    public class DamageEffectAction : IEffectAction
    {
        public bool Apply(EffectTarget target, EffectDefinition definition, object token) => target.ReceiveDamage(definition.Value);
        public void Remove(EffectTarget target, object token) { }
    }

    public class KnockbackEffectAction : IEffectAction
    {
        public bool Apply(EffectTarget target, EffectDefinition definition, object token)
        { target.SetKnockbackRate(token, definition.Value); return true; }
        public void Remove(EffectTarget target, object token) => target.RemoveKnockbackRate(token);
    }
}
