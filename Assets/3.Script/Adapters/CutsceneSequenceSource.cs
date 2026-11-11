using System;
using System.Linq;
using HumanGhost.Data.Storage;

namespace HumanGhost.Gameplay.Adapters
{
    // 검사된 테이블을 재생 코어의 정의로 연결합니다. 리소스 로드나 게임 진행은 수행하지 않습니다.
    public static class CutsceneSequenceSource
    {
        // 콘텐츠 번호 대신 기존 표의 역할 키를 조회합니다. 행이 없으면 제작 대기입니다.
        public const string IntroKey = "Intro", LedgerKey = "Ledger", EndingKey = "Ending";
        public static int FindId(IReadOnlyDataCatalog data, string key)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            return data.GetTable<Data.Definitions.Cutscene>().All.SingleOrDefault(row => row.Key == key)?.ID ?? 0;
        }
        public static CutsceneSequence Create(IReadOnlyDataCatalog data, int cutsceneId)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            var definition = data.GetTable<Data.Definitions.Cutscene>().Get(cutsceneId);
            var steps = data.GetTable<Data.Definitions.CutsceneStep>().All
                .Where(step => step.CutsceneID == cutsceneId).OrderBy(step => step.Order)
                .Select(step => new CutsceneStep(Enum.Parse<CutsceneMedia>(step.MediaType),
                    step.ResourceAddress, step.DurationMs, step.TextID ?? 0, step.CanAdvance));
            return new CutsceneSequence(definition.Key, steps);
        }
    }
}
