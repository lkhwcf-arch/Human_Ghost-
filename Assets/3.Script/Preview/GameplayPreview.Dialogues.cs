using System;
using System.Collections.Generic;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay.Adapters;
using HumanGhost.Gameplay.Presentation;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    public sealed partial class GameplayPreview
    {
        [SerializeField, InspectorName("NPC 이름표 글꼴")] private Font _npcNameFont = null;
        // NPC 이름표 공용 배치 설정입니다(모든 NPC 공통, 2026-11-17). 간격은 tile_x100, 배율은 분모 10000 정수입니다.
        // 기본값은 기획 요청 제안값(머리 위 0.3칸·기존 2배)이며 확정 시 이 칸에서 조정합니다.
        [SerializeField, InspectorName("NPC 이름표 머리 위 간격 (tile_x100)"), Min(0)] private int _npcNameGapUnits = 30;
        [SerializeField, InspectorName("NPC 이름표 글자 배율 (10000 = 기존 크기)"), Min(1)] private int _npcNameScaleRatio = 20000;
        [SerializeField, InspectorName("NPC 이름표 위 말걸기 안내 간격 (tile_x100)"), Min(0)] private int _npcPromptGapUnits = 10;
        private DialogueRunner _dialogues;
        private LedgerInteract _ledger;
        private SubscriptionScope _dialogueScope;
        private readonly Queue<int> _dialogueQueue = new Queue<int>();
        private readonly HashSet<int> _queuedDialogues = new HashSet<int>();
        private bool _dialogueFailed;
        private bool HasPendingDialogue => _dialogueFailed || _dialogueQueue.Count > 0 || (_dialogues != null && _dialogues.IsRunning);

        private void ConnectDialogues()
        {
            DisconnectDialogues();
            if (_textbox == null || _data == null || _data.Assets == null)
                throw new InvalidOperationException("데이터 대화에 사용할 화면과 JSON 리소스 목록을 연결해 주세요.");
            _dialogueFailed = false;
            _dialogues = new DialogueRunner(_catalog, _data.Assets.JsonFiles, _textbox, paused =>
            {
                if (_speed == null && paused) throw new InvalidOperationException("대화 정지에 사용할 공통 게임 속도를 연결해 주세요.");
                if (_speed != null) _speed.State.SetPauseOwner(this, paused);
            }, error => { _dialogueFailed = true; ReportCutsceneError(error); });
            if (_stage != null)
            {
                var layout = new NpcNameLayout(_npcNameGapUnits, _npcNameScaleRatio, _npcPromptGapUnits, _worldUnitsPerTile);
                foreach (NpcTalk npc in _stage.MapRoot.GetComponentsInChildren<NpcTalk>(true))
                    if (npc.NpcId != 0) npc.Configure(npc.NpcId, _catalog, _dialogues, _playerLabel, _npcNameFont, layout);
            }
            // 명부: 조사 종류와 현재 Stage로 대화·해금 Skill을 데이터에서 찾습니다. 누락·중복은 맵 준비 실패로 전달합니다.
            if (_ledger != null)
                _ledger.Configure(new LedgerInvestigation(LedgerInvestigationDefinition.Resolve(_stageId,
                    _catalog.GetTable<Dialogue>().All, _catalog.GetTable<Skill>().All), _progress.Progress), _dialogues);
            _dialogueScope = new SubscriptionScope();
            if (!_bossActive) return;
            BossPoise boss = _boss.Actor;
            Action<BossState> phase = _dialogueScope.Guard<BossState>(row => QueueDialogue("BOSS_STATE_ENTER", row.ID));
            Action changed = _dialogueScope.Guard(() => { if (boss.IsDead) QueueDialogue("BOSS_DEFEATED", boss.Definition.ID); });
            _dialogueScope.Add(() => boss.PhaseChanged -= phase); boss.PhaseChanged += phase;
            _dialogueScope.Add(() => boss.Changed -= changed); boss.Changed += changed;
            QueueDialogue("BOSS_STATE_ENTER", boss.Phase.ID);
        }
        private void QueueDialogue(string trigger, int target)
        {
            // 전환 대사는 포효와 무적을 소유한 StepBossPhase 한 곳에서만 실행합니다.
            if (trigger == "BOSS_STATE_ENTER" && _bossPhaseDialogue != null && target == _bossPhaseDialogue.TriggerTargetID) return;
            foreach (Dialogue row in _catalog.GetTable<Dialogue>().All)
                if (row.TriggerType == trigger && row.TriggerTargetID == target && _queuedDialogues.Add(row.ID)) _dialogueQueue.Enqueue(row.ID);
        }
        private void StepDialogues()
        {
            if (_dialogues == null || _dialogueFailed) return;
            try
            {
                _dialogues.Step();
                if (!_subscribed || _dialogues.IsRunning || _dialogueQueue.Count == 0 || _modalBlocked || _saveLocked || _leaving ||
                    _actor.IsDead || _actor.CurrentState == StateKey.Execute || _textbox.IsOpen || LoadingScreen.IsActive) return;
                int id = _dialogueQueue.Dequeue(); _queuedDialogues.Remove(id);
                _dialogues.TryPlay(id, this);
            }
            catch (Exception error)
            {
                _dialogueFailed = true;
                _dialogues.Dispose();
                ReportCutsceneError(new InvalidOperationException("대화 연결 또는 실행에 실패했습니다.", error));
            }
        }
        // 대화 실행기 Step 다음 순서에서 명부 요청 결과를 소비합니다. 실패는 대화 실행기와 분리해 보고합니다.
        private void StepLedger()
        {
            if (_ledger == null) return;
            try { _ledger.StepResult(); }
            catch (Exception error) { ReportCutsceneError(new InvalidOperationException("명부 조사 완료 적용에 실패했습니다.", error)); }
        }
        private void DisconnectDialogues()
        {
            var scope = _dialogueScope; _dialogueScope = null;
            LedgerInteract ledger = _ledger;
            _dialogueQueue.Clear(); _queuedDialogues.Clear();
            DialogueRunner dialogues = _dialogues;
            _dialogues = null;
            SubscriptionScope.Cleanup(() => scope?.Dispose(), () => { if (ledger != null) ledger.Disconnect(); }, () => dialogues?.Dispose(),
                () => { if (_speed != null) _speed.State.SetPauseOwner(this, false); });
        }
    }
}
