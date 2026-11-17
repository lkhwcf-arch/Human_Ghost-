using System;
using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    // Ledger(K15): 처음 조사하면 데이터의 명부 대화를 공용 대화 실행기로 재생합니다.
    // 마지막 줄까지 정상 종료된 요청만 조사 완료(Stage 클리어)·스킬 해금으로 이어집니다. 이미 조사했으면 재조사 문구만 표시합니다.
    // 대화 ID·Stage·Skill은 조립자가 데이터에서 찾아 LedgerInvestigation으로 넣어 줍니다.
    public class LedgerInteract : InteractableBehaviour
    {
        // UI_Lang 연결 키입니다. 실제 문구는 Language 표에서 고릅니다.
        private const int RepeatNoticeUiId = 52005, UnlockNoticeUiId = 52006;
        private LedgerInvestigation _investigation;
        private DialogueRunner _dialogues;
        private SubscriptionScope _dialogueScope;
        private InteractionContext _context;

        public event Action SkillUnlocked;
        public void Configure(LedgerInvestigation investigation, DialogueRunner dialogues)
        {
            if (investigation == null) throw new ArgumentNullException(nameof(investigation));
            if (dialogues == null) throw new ArgumentNullException(nameof(dialogues));
            Disconnect();
            _investigation = investigation; _dialogues = dialogues;
            ConnectDialogueEvents();
            NotifyChanged();
        }

        // 조립자가 실행기를 정리하기 전에 호출합니다. 공용 실행기는 Dispose하지 않고 자기 요청만 취소합니다.
        public void Disconnect()
        {
            ReleaseRequest();
            _investigation = null; _dialogues = null;
            NotifyChanged();
        }

        public override bool CanInteract => _investigation != null && _dialogues != null && !_investigation.IsWaiting
            && (_investigation.IsInvestigated || _dialogues.CanPlay(_investigation.Definition.DialogueId));
        public override int PromptTextId => 30065;

        public override void Interact(InteractionContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (_investigation == null || _dialogues == null)
                throw new InvalidOperationException(name + "에 명부 조사 데이터와 대화 실행기를 연결해 주세요.");
            if (_investigation.IsWaiting) return;
            if (_investigation.IsInvestigated)
            {
                context.Textbox.ShowNotice(LanguageTextSource.SelectUi(context.Data, RepeatNoticeUiId));
                return;
            }
            // 다른 대화 재생 중 등 시작 거절은 진행을 바꾸지 않고 조사 가능 상태로 둡니다.
            if (!_dialogues.TryPlay(_investigation.Definition.DialogueId, this, out int requestId)) return;
            _context = context;
            _investigation.Wait(requestId);
            NotifyChanged();
        }

        // 조립자가 대화 실행기 Step 다음 순서에서 호출합니다. 이벤트 콜백이 아닌 곳에서만 진행 명령을 실행합니다.
        public void StepResult()
        {
            LedgerInvestigation investigation = _investigation;
            if (investigation == null || !investigation.IsWaiting) return;
            if (_dialogues == null || !isActiveAndEnabled) { ReleaseRequest(); NotifyChanged(); return; }
            int requestId = investigation.RequestId;
            if (!_dialogues.TryTakeResult(this, requestId, out DialogueEndReason reason)) return;
            InteractionContext context = _context;
            _context = null;
            try
            {
                LedgerOutcome outcome = investigation.Finish(requestId, reason,
                    skill => context.Player.EnableSkill(skill, new ContentAccess(context.Progress.IsCleared)));
                if (outcome == LedgerOutcome.Unlocked)
                {
                    context.Textbox.ShowNotice(LanguageTextSource.SelectUi(context.Data, UnlockNoticeUiId));
                    SkillUnlocked?.Invoke();
                }
            }
            finally { NotifyChanged(); }
        }

        private void ConnectDialogueEvents()
        {
            if (!isActiveAndEnabled || _dialogues == null || _dialogueScope != null) return;
            _dialogueScope = new SubscriptionScope();
            DialogueRunner runner = _dialogues;
            // 사용 가능 통지는 표시 갱신에만 씁니다. 완료 판정은 StepResult의 요청 결과로 합니다.
            Action changed = _dialogueScope.Guard(() =>
            {
                try { if (this != null && isActiveAndEnabled) NotifyChanged(); }
                catch (Exception error) { Debug.LogException(error, this); }
            });
            _dialogueScope.Add(() => runner.AvailabilityChanged -= changed); runner.AvailabilityChanged += changed;
        }

        // 구독 해제·자기 요청 취소·대기 결과 폐기를 한 곳에서 끝냅니다.
        private void ReleaseRequest()
        {
            var scope = _dialogueScope; _dialogueScope = null;
            DialogueRunner dialogues = _dialogues;
            _context = null;
            _investigation?.Abandon();
            SubscriptionScope.Cleanup(() => scope?.Dispose(), () => dialogues?.Cancel(this));
        }

        protected override void OnEnable() { base.OnEnable(); ConnectDialogueEvents(); }
        protected override void OnDisable() { ReleaseRequest(); base.OnDisable(); }
    }
}
