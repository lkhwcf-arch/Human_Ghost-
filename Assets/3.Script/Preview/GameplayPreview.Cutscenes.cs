using System;
using HumanGhost.Gameplay.Adapters;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 맵 흐름만 연결합니다. 준비·재생·입력·모달 수명은 공용 실행 담당이 재사용합니다.
    public sealed partial class GameplayPreview
    {
        [SerializeField, InspectorName("공용 컷씬 실행 담당")] private CutsceneHost _cutscenes = null;
        private int _ledgerCutsceneId, _endingCutsceneId;
        private void ConnectCutsceneData()
        {
            _ledgerCutsceneId = CutsceneSequenceSource.FindId(_catalog, CutsceneSequenceSource.LedgerKey);
            _endingCutsceneId = CutsceneSequenceSource.FindId(_catalog, CutsceneSequenceSource.EndingKey);
        }
        private Action _afterCutscene;
        private int _pendingCutsceneId;
        private bool _endingAttempted;

        private bool PlayLedger(Action complete) => BeginCutscene(_ledgerCutsceneId, complete);
        private bool BeginCutscene(int id, Action complete)
        {
            if (_afterCutscene != null || _modalBlocked || _leaving || _saveLocked || _actor.IsDead) return false;
            if (_cutscenes == null) throw new InvalidOperationException("컷씬 ID를 사용하는 맵에는 공용 컷씬 실행 담당을 연결해 주세요.");
            if (!_cutscenes.TryPlay(_catalog, id, _audio)) return false;
            _actor.SetCutsceneDamageBlocked(true);
            _pendingCutsceneId = id;
            _afterCutscene = complete;
            return true;
        }
        private void StepCutsceneFlow()
        {
            if (_afterCutscene == null) return;
            CutsceneEndReason reason = CutsceneEndReason.Cancelled;
            if (_cutscenes != null && !_cutscenes.TryTakeResult(out reason)) return;
            Action complete = _afterCutscene;
            _afterCutscene = null;
            _actor.SetCutsceneDamageBlocked(false);
            int id = _pendingCutsceneId;
            _pendingCutsceneId = 0;
            if (!reason.AllowsProgress()) return;
            _progress.MarkCutsceneViewed(id);
            try { complete(); }
            catch (Exception error) { Debug.LogException(new InvalidOperationException("컷씬 종료 후 진행 연결에 실패했습니다.", error), this); }
        }
        private void TryStartEnding()
        {
            if (_endingAttempted || _endingCutsceneId == 0 || !_bossActive || !_boss.Actor.IsDead
                || _afterCutscene != null || _modalBlocked || _saveLocked || _leaving || _actor.IsDead
                || _actor.CurrentState == HumanGhost.Data.Definitions.StateKey.Execute) return;
            try
            {
                if (_loadingPrefab == null) throw new InvalidOperationException("엔딩 후 타이틀 암전 이동 화면을 연결해 주세요.");
                _endingAttempted = BeginCutscene(_endingCutsceneId, ReturnToTitle);
            }
            catch (Exception error) { _endingAttempted = true; Debug.LogException(error, this); }
        }
        private void DisconnectCutscenes()
        {
            if (_afterCutscene != null) _actor?.SetCutsceneDamageBlocked(false);
            if (_afterCutscene != null && _cutscenes != null)
            {
                _cutscenes.Cancel();
                _cutscenes.TryTakeResult(out _);
            }
            _afterCutscene = null;
            _pendingCutsceneId = 0;
        }
    }
}
