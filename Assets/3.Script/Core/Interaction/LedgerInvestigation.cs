using System;
using System.Collections.Generic;
using System.Linq;
using HumanGhost.Data.Definitions;

namespace HumanGhost.Gameplay
{
    // 명부 조사 데이터 연결입니다. 조사 종류와 Stage ID로 대화 하나와 해금 Skill 하나를 결정합니다.
    // 콘텐츠 ID(대화·Stage·Skill)는 코드에 두지 않고 표에서 찾습니다.
    public sealed class LedgerInvestigationDefinition
    {
        public const string TriggerType = "LEDGER_INVESTIGATE";
        public const string SkillUnlockType = "ROOM_LEDGER";
        // ContentAccess의 "Stage 클리어" 조건 번호입니다.
        private const int StageClearedCondition = 1;

        public int StageId { get; }
        public int DialogueId { get; }
        public Skill Skill { get; }

        private LedgerInvestigationDefinition(int stageId, int dialogueId, Skill skill)
        { StageId = stageId; DialogueId = dialogueId; Skill = skill; }

        // 누락·중복이면 첫 행 선택이나 즉시 해금으로 대체하지 않고 조립자에게 실패를 전달합니다.
        public static LedgerInvestigationDefinition Resolve(int stageId, IEnumerable<Dialogue> dialogues, IEnumerable<Skill> skills)
        {
            if (dialogues == null) throw new ArgumentNullException(nameof(dialogues));
            if (skills == null) throw new ArgumentNullException(nameof(skills));
            Dialogue[] talks = dialogues.Where(row => row.TriggerType == TriggerType && row.TriggerTargetID == stageId).ToArray();
            if (talks.Length != 1)
                throw new InvalidOperationException($"명부 대화는 Stage마다 하나여야 합니다. Dialogue TriggerType={TriggerType}, TriggerTargetID={stageId}, 찾은 행 {talks.Length}개");
            Skill[] unlocks = skills.Where(row => row.UnlockType == SkillUnlockType && row.UnlockTargetID == stageId).ToArray();
            if (unlocks.Length != 1)
                throw new InvalidOperationException($"명부 해금 Skill은 Stage마다 하나여야 합니다. Skill UnlockType={SkillUnlockType}, UnlockTargetID={stageId}, 찾은 행 {unlocks.Length}개");
            Skill skill = unlocks[0];
            // 조사 완료(Stage 클리어) 직후 해금 조건이 충족돼야 진행과 스킬이 어긋나지 않습니다.
            if (skill.UnlockConditionType != StageClearedCondition || skill.UnlockRequiredValue > 1)
                throw new InvalidOperationException($"명부 해금 Skill {skill.ID}의 조건은 Stage {stageId} 클리어여야 합니다.");
            return new LedgerInvestigationDefinition(stageId, talks[0].ID, skill);
        }
    }

    public enum LedgerOutcome
    {
        // 오래된 요청·취소·실패: 진행을 바꾸지 않습니다.
        Ignored,
        // 다른 경로로 이미 조사 완료된 상태였습니다.
        AlreadyInvestigated,
        // 조사 완료를 기록했지만 기획 설정(ContentEnabled 등)으로 스킬은 해금하지 않았습니다.
        Investigated,
        // 조사 완료와 스킬 해금을 모두 적용했습니다.
        Unlocked,
    }

    // 명부 조사의 실행 상태입니다. 원본 정의는 읽기 전용이고 대기 중인 요청 번호만 소유합니다.
    // 조사 완료(Stage 클리어)가 원본 진행이며 스킬 해금은 진행에서 다시 계산되는 파생 상태입니다.
    public sealed class LedgerInvestigation
    {
        private readonly StageProgress _progress;
        public LedgerInvestigationDefinition Definition { get; }
        public int RequestId { get; private set; }
        public bool IsWaiting => RequestId != 0;
        public bool IsInvestigated => _progress.IsCleared(Definition.StageId);

        public LedgerInvestigation(LedgerInvestigationDefinition definition, StageProgress progress)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _progress = progress ?? throw new ArgumentNullException(nameof(progress));
        }

        public void Wait(int requestId)
        {
            if (requestId <= 0) throw new ArgumentOutOfRangeException(nameof(requestId));
            if (IsWaiting) throw new InvalidOperationException("명부 조사 대화를 이미 기다리고 있습니다.");
            RequestId = requestId;
        }

        public void Abandon() => RequestId = 0;

        // 정상 완료한 현재 요청만 적용합니다. 진행을 먼저 기록하고 스킬을 해금합니다.
        // 해금 도중 예외가 나도 진행은 남으며, 다음 맵 준비에서 진행 기반 스킬 해금이 다시 적용됩니다.
        public LedgerOutcome Finish(int requestId, DialogueEndReason reason, Func<Skill, bool> enableSkill)
        {
            if (enableSkill == null) throw new ArgumentNullException(nameof(enableSkill));
            if (!IsWaiting || requestId != RequestId) return LedgerOutcome.Ignored;
            RequestId = 0;
            if (reason != DialogueEndReason.Completed) return LedgerOutcome.Ignored;
            if (!_progress.Clear(Definition.StageId)) return LedgerOutcome.AlreadyInvestigated;
            return enableSkill(Definition.Skill) ? LedgerOutcome.Unlocked : LedgerOutcome.Investigated;
        }
    }
}
