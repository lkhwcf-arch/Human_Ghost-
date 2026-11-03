using System;
using HumanGhost.Data.Definitions;

namespace HumanGhost.Gameplay
{
    public enum ContentAccessResult
    {
        Allowed,
        DisabledByPlanner,
        UnlockRequired
    }

    // 진행 기록은 호출자가 소유합니다. 판단할 때 최신 기록을 조회하며 결과를 저장하지 않습니다.
    public sealed class ContentAccess
    {
        private readonly Func<int, bool> _isStageCleared;

        public ContentAccess(Func<int, bool> isStageCleared)
        {
            _isStageCleared = isStageCleared ?? throw new ArgumentNullException(nameof(isStageCleared));
        }

        public ContentAccessResult Evaluate(Stage definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            return Evaluate(definition.ContentEnabled, definition.UnlockConditionType,
                definition.UnlockTargetID, definition.UnlockRequiredValue);
        }

        public ContentAccessResult Evaluate(Skill definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            return Evaluate(definition.ContentEnabled, definition.UnlockConditionType,
                definition.UnlockTargetID, definition.UnlockRequiredValue);
        }

        public ContentAccessResult Evaluate(Item definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            return Evaluate(definition.ContentEnabled, 0, 0, 0);
        }

        public ContentAccessResult Evaluate(Monster definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            return Evaluate(definition.ContentEnabled, 0, 0, 0);
        }

        // Active·UnlockType 등 기능 고유의 조건은 각 실행 담당이 처리합니다.
        private ContentAccessResult Evaluate(bool enabled, int conditionType, int targetId, int requiredValue)
        {
            if (!enabled) return ContentAccessResult.DisabledByPlanner;
            switch (conditionType)
            {
                case 0:
                    return ContentAccessResult.Allowed;
                case 1:
                    return (_isStageCleared(targetId) ? 1 : 0) >= requiredValue
                        ? ContentAccessResult.Allowed : ContentAccessResult.UnlockRequired;
                default:
                    // 지원하지 않는 조건은 호출자에게 구현 누락을 알립니다.
                    throw new NotSupportedException("지원하지 않는 콘텐츠 해금 조건입니다: " + conditionType);
            }
        }
    }
}
