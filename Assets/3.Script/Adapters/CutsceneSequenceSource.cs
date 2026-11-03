using System;
using System.Linq;
using HumanGhost.Data.Storage;

namespace HumanGhost.Gameplay.Adapters
{
    // 검사된 테이블을 재생 코어의 정의로 연결합니다. 리소스 로드나 게임 진행은 수행하지 않습니다.
    public static class CutsceneSequenceSource
    {
        public static CutsceneSequence Create(IReadOnlyDataCatalog data, int cutsceneId)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            var definition = data.GetTable<Data.Definitions.Cutscene>().Get(cutsceneId);
            var steps = data.GetTable<Data.Definitions.CutsceneStep>().All
                .Where(step => step.CutsceneID == cutsceneId).OrderBy(step => step.Order)
                .Select(step => new CutsceneStep(Enum.Parse<CutsceneMedia>(step.MediaType),
                    step.ResourceAddress, step.DurationMs, step.TextID ?? 0, step.CanAdvance));
            return new CutsceneSequence(definition.Key, steps, definition.SkipHoldMs);
        }
    }
}
