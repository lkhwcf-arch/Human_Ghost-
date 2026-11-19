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
        private int _bossPhaseElapsedMilliseconds;
        private int? _bossMusicPhase;
        private const int _bossMusicFadeInMilliseconds = 2000, _bossMusicFadeOutMilliseconds = 3000;

        private void StepBossMusic()
        {
            if (_audio == null) return;
            BossPoise boss = _boss.Actor;
            int? phase = boss.IsDead || boss.IsTransitioning ? (int?)null : boss.Phase.Phase;
            if (phase == _bossMusicPhase) return;

            if (_bossMusicPhase.HasValue) _audio.FadeOutMusic(_bossMusicFadeOutMilliseconds);
            _bossMusicPhase = phase;
            if (phase == 0) _audio.PlayMusic("BGM_BOSS_PHASE1", _bossMusicFadeInMilliseconds);
            if (phase == 1) _audio.PlayMusic("BGM_BOSS_PHASE2", _bossMusicFadeInMilliseconds);
        }
        private BossPhaseTwoView _bossView;
        private void ConnectBossPhaseData()
        {
            _bossView = _boss.GetComponent<BossPhaseTwoView>();
            _bossView?.ConnectCamera(_followCamera);
            _bossView?.ConnectWalkStep(() => _audio?.PlayEffect("BOSS_2_WALK"));
            _bossView?.ConnectWarningOrigin(() => _bossDriver != null ? _bossDriver.WarningOriginX : _boss.ToWorld(_boss.Position.X));
            // 2페이즈 대사는 이 보스의 첫 분노 상태(Phase 1 이상 중 가장 앞) 진입 대사입니다. 상태 ID를 코드에 두지 않습니다(11/19, 조건 규칙 감사 3.9).
            int phaseTwoState = _boss.Actor.Definition.States.Where(state => state.Phase > 0).OrderBy(state => state.Phase).First().ID;
            _bossPhaseDialogue = _catalog.GetTable<Dialogue>().All.Single(row => row.TriggerType == "BOSS_STATE_ENTER" && row.TriggerTargetID == phaseTwoState);
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
                _bossPhaseRunning = true; _bossPhaseElapsedMilliseconds = 0; _bossPhaseLine = 0;
                _actor.SetCutsceneDamageBlocked(true); SetGameInputLocked(true);
                _followCamera?.Shake(.35f,250);
                _bossView?.FlashTransition();
            }
            if (_modalBlocked || _saveLocked || _leaving || LoadingScreen.IsActive) return true;
            _bossPhaseElapsedMilliseconds += Math.Min(_gameElapsedMilliseconds, 700 - _bossPhaseElapsedMilliseconds);
            if (_bossPhaseElapsedMilliseconds < 700) return true;
            if (!_progress.HasViewedDialogue(_bossPhaseDialogue.ID) && _bossPhaseLine < _bossPhaseLines.Length)
            {
                if (!_bossPhasePaused) { _bossPhasePaused = true; _speed.State.SetPaused(true); }
                if (!_textbox.IsOpen)
                {
                    var line = _bossPhaseLines[_bossPhaseLine];
                    _textbox.Open(new[] { new DialogueLine(line.SpeakerTextID, line.TextID) }, new LanguageTextSource(_catalog), LanguageSettings.Current, true, () => _bossPhaseLine++);
                }
                return true;
            }
            _progress.MarkDialogueViewed(_bossPhaseDialogue.ID);
            DisconnectBossPhase();
            _boss.Actor.CompletePhaseTransition();
            _bossView?.FlashTransition();
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
