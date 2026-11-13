using System;
using System.Collections.Generic;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay.Effects;

namespace HumanGhost.Gameplay.Adapters
{
    // 현재 시트 해석만 담당합니다. 실행 상태는 이곳에서 만들거나 공유하지 않습니다.
    public static class FieldEffectData
    {
        public static IReadOnlyList<EffectDefinition> Read(FieldObject field)
        {
            switch (ReadKind(field.Kind))
            {
                case FieldKind.FireStatic: return new[] { new EffectDefinition(field.ID, EffectExecution.Periodic,
                    EffectOperation.Damage, field.Damage, field.TickMs, category: EffectCategory.Fire) };
                case FieldKind.Ice: return new[] { new EffectDefinition(field.ID, EffectExecution.Maintained,
                    EffectOperation.KnockbackRate, field.KnockbackRate, category: EffectCategory.Ice) };
                default: throw new InvalidOperationException("필드 효과 실행 담당이 없습니다: " + field.Kind);
            }
        }
        public static FieldKind ReadKind(string value) => FieldKindKeys.Parse(value);
        public static IEnumerable<EffectCategory> ReadImmunities(string value)
        {
            foreach (string key in (value ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                EffectCategory category = EffectCategoryKeys.Parse(key.Trim());
                yield return category;
            }
        }
    }
}
