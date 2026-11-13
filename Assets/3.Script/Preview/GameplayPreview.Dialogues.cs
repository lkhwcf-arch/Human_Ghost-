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
        private DialogueRunner _dialogues;
        private SubscriptionScope _dialogueScope;
        private readonly Queue<int> _dialogueQueue = new Queue<int>();
        private readonly HashSet<int> _queuedDialogues = new HashSet<int>();
        private bool _dialogueFailed;
        private bool HasPendingDialogue => _dialogueFailed || _dialogueQueue.Count > 0 || (_dialogues != null && _dialogues.IsRunning);

        private void ConnectDialogues()
        {
            if (_textbox == null || _data.Assets == null) return;
            _dialogueFailed = false;
            _dialogues = new DialogueRunner(_catalog, _data.Assets.JsonFiles, _textbox, paused =>
            {
                if (_speed == null && paused) throw new InvalidOperationException("대화 정지에 사용할 공통 게임 속도를 연결해 주세요.");
                if (_speed != null) _speed.State.SetPauseOwner(this, paused);
            }, error => { _dialogueFailed = true; ReportCutsceneError(error); });
            if (_stage != null)
                foreach (NpcTalk npc in _stage.MapRoot.GetComponentsInChildren<NpcTalk>(true))
                    if (npc.NpcId != 0) npc.Configure(npc.NpcId, _catalog, _dialogues, _playerLabel);
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
        private void DisconnectDialogues()
        {
            _dialogueScope?.Dispose(); _dialogueScope = null;
            _dialogueQueue.Clear(); _queuedDialogues.Clear();
            _dialogues?.Dispose(); _dialogues = null;
            if (_speed != null) _speed.State.SetPauseOwner(this, false);
        }
    }
}
