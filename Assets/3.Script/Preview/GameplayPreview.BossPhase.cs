using System;
using System.Linq;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay.Adapters;
using HumanGhost.Gameplay.Presentation;
using UnityEngine;
namespace HumanGhost.Gameplay.Preview
{
    public sealed partial class GameplayPreview
    {
        [Serializable] private sealed class BossLine { public int SpeakerTextID = 0, TextID = 0; }
        [Serializable] private sealed class BossDialogue { public int DialogueID = 0; public BossLine[] Lines = null; }
        private Dialogue _bossPhaseDialogue;
        private BossLine[] _bossPhaseLines;
        private bool _bossPhaseRunning, _bossPhasePaused;
        private int _bossPhaseLine;
        private float _bossPhaseElapsed;
        private void ConnectBossPhaseData()
        {
            _boss.GetComponent<BossPhaseTwoView>()?.ConnectCamera(_followCamera);
            _bossPhaseDialogue = _catalog.GetTable<Dialogue>().All.Single(row => row.TriggerType == "BOSS_STATE_ENTER" && row.TriggerTargetID == 4002);
            string file = System.IO.Path.GetFileNameWithoutExtension(_bossPhaseDialogue.LineFile);
            var asset = _data.Assets.JsonFiles.Single(row => row != null && row.name == file);
            var lines = JsonUtility.FromJson<BossDialogue>(asset.text);
            if (lines == null || lines.DialogueID != _bossPhaseDialogue.ID || lines.Lines == null || lines.Lines.Length == 0 ||
                lines.Lines.Any(line => line == null || line.TextID <= 0 || line.SpeakerTextID <= 0))
                throw new InvalidOperationException("보스 2페이즈 대사 데이터가 유효하지 않습니다.");
            foreach (var line in lines.Lines)
            { _catalog.GetTable<Language>().Get(line.TextID); _catalog.GetTable<Language>().Get(line.SpeakerTextID); }
            _bossPhaseLines = lines.Lines;
        }
        private bool StepBossPhase()
        {
            if (!_subscribed || !_bossActive || !_boss.Actor.IsTransitioning) return false;
            if (!_bossPhaseRunning)
            {
                if (_modalBlocked || _saveLocked || _leaving || LoadingScreen.IsActive || (_textbox != null && _textbox.IsOpen)) return true;
                _bossPhaseRunning = true; _bossPhaseElapsed = 0f; _bossPhaseLine = 0;
                _actor.SetCutsceneDamageBlocked(true); SetGameInputLocked(true);
                _followCamera?.Shake(.35f,.25f);
                _boss.GetComponent<BossPhaseTwoView>()?.FlashTransition();
            }
            if (_modalBlocked || _saveLocked || _leaving || LoadingScreen.IsActive) return true;
            _bossPhaseElapsed += Time.deltaTime;
            if (_bossPhaseElapsed < .7f) return true;
            if (!_progress.HasViewedDialogue(_bossPhaseDialogue.ID) && _bossPhaseLine < _bossPhaseLines.Length)
            {
                if (!_bossPhasePaused) { _bossPhasePaused = true; _speed.State.SetPaused(true); }
                if (!_textbox.IsOpen)
                {
                    var line = _bossPhaseLines[_bossPhaseLine];
                    _textbox.Open(line.SpeakerTextID, new[] { line.TextID }, new LanguageTextSource(_catalog), LanguageCode.Korean, () => _bossPhaseLine++);
                }
                return true;
            }
            _progress.MarkDialogueViewed(_bossPhaseDialogue.ID);
            DisconnectBossPhase();
            _boss.Actor.CompletePhaseTransition();
            _boss.GetComponent<BossPhaseTwoView>()?.FlashTransition();
            return false;
        }
        private void DisconnectBossPhase()
        {
            if (_bossPhasePaused && _speed != null) _speed.State.SetPaused(false);
            _bossPhasePaused = false;
            if (!_bossPhaseRunning) return;
            _bossPhaseRunning = false;
            _actor?.SetCutsceneDamageBlocked(false);
            if (_input != null && _actor != null) SetGameInputLocked(false);
        }
    }
}
