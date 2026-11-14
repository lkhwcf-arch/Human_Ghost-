using System;
using HumanGhost.Gameplay.Adapters;
using HumanGhost.Gameplay.Presentation;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 맵 흐름만 연결합니다. 준비·재생·입력·모달 수명은 공용 실행 담당이 재사용합니다.
    public sealed partial class GameplayPreview
    {
        [SerializeField, InspectorName("공용 컷씬 실행 담당")] private CutsceneHost _cutscenes = null;
        [SerializeField, InspectorName("공용 컷씬 프리팹")] private CutsceneHost _cutscenePrefab = null;
        [SerializeField, InspectorName("시연 마무리 문구 Language ID")] private int[] _endingTextIds = { 30352 };
        [SerializeField, InspectorName("보스 처치 뒤 엔딩 대기(밀리초)"), Min(0)] private int _endingDelayMs = 2000;
        [SerializeField, InspectorName("마무리 문구 표시 시간(밀리초)"), Min(1)] private int _endingNoticeMs = 3000;
        private float _endingWaitSeconds;
        private GameObject _cutsceneRoot;
        private DemoEndingFlow _ending;
        private bool _endingReturnPending;
        private int _ledgerCutsceneId, _endingCutsceneId;
        private void ConnectCutsceneData()
        {
            _ledgerCutsceneId = CutsceneSequenceSource.FindId(_catalog, CutsceneSequenceSource.LedgerKey);
            _endingCutsceneId = CutsceneSequenceSource.FindId(_catalog, CutsceneSequenceSource.EndingKey);
            bool viewed = _endingCutsceneId != 0 && Array.IndexOf(_progress.ViewedCutscenes, _endingCutsceneId) >= 0;
            _ending = _stageId == BossStage ? new DemoEndingFlow(viewed && _progress.HasDefeatedBoss(BossStage)) : null;
        }
        private Action _afterCutscene;
        private int _pendingCutsceneId;

        private bool PlayLedger(Action complete) => BeginCutscene(_ledgerCutsceneId, complete);
        private bool BeginCutscene(int id, Action complete)
        {
            if (_afterCutscene != null || _modalBlocked || _leaving || _saveLocked || _actor.IsDead) return false;
            if (_cutscenes == null)
                _cutscenes = CutsceneHost.Create(_cutscenePrefab, transform, _shrineModalBinding, out _cutsceneRoot);
            if (!_cutscenes.TryPlay(_catalog, id, _audio)) return false;
            _actor.SetCutsceneDamageBlocked(true);
            _pendingCutsceneId = id;
            _afterCutscene = complete;
            return true;
        }
        private void StepCutsceneFlow()
        {
            if (_endingReturnPending && !_modalBlocked && !LoadingScreen.IsActive)
            {
                _endingReturnPending = false;
                try { ReturnToTitle(); }
                catch (Exception error) { FailEnding(error); }
            }
            if (_afterCutscene == null) return;
            CutsceneEndReason reason = CutsceneEndReason.Cancelled;
            if (_cutscenes != null && !_cutscenes.TryTakeResult(out reason)) return;
            Action complete = _afterCutscene;
            _afterCutscene = null;
            _actor.SetCutsceneDamageBlocked(false);
            int id = _pendingCutsceneId;
            _pendingCutsceneId = 0;
            if (!reason.AllowsProgress())
            {
                if (id == _endingCutsceneId)
                {
                    _ending?.EndMovie(reason);
                    if (reason == CutsceneEndReason.Failed) ReportCutsceneError(new InvalidOperationException("엔딩 재생에 실패했습니다. 영상 파일과 재생 로그를 확인해 주세요."));
                }
                return;
            }
            _progress.MarkCutsceneViewed(id);
            try { complete(); }
            catch (Exception error) { FailEnding(new InvalidOperationException("컷씬 종료 후 진행 연결에 실패했습니다.", error)); }
        }
        private void TryStartEnding()
        {
            if (HasPendingDialogue || _ending == null || _ending.Phase != DemoEndingPhase.Waiting || _endingCutsceneId == 0 || !_progress.HasDefeatedBoss(BossStage)
                || _afterCutscene != null || _modalBlocked || _saveLocked || _leaving || _actor.IsDead
                || (_textbox != null && _textbox.IsOpen)
                || _actor.CurrentState == HumanGhost.Data.Definitions.StateKey.Execute) return;
            // 1114 A2: 처치(처형·대화 종료 포함) 뒤 정해진 시간이 지나야 영상을 시작합니다.
            _endingWaitSeconds += Time.deltaTime;
            if (_endingWaitSeconds * 1000f < _endingDelayMs) return;
            try
            {
                if (BeginCutscene(_endingCutsceneId, () => _ending.EndMovie(CutsceneEndReason.Completed)))
                    _ending.TryBegin(true);
            }
            catch (Exception error) { FailEnding(error); }
        }

        private bool CanFinishDemo() => _ending != null && _ending.CanFinish;
        private void FinishDemo()
        {
            if (!CanFinishDemo() || _textbox == null || _textbox.IsOpen || _modalBlocked || _saveLocked || _leaving || _actor.IsDead) return;
            try
            {
                if (_loadingPrefab == null) throw new InvalidOperationException("타이틀 복귀 화면을 연결해 주세요.");
                if (!_ending.TryFinish()) return;
                DemoEndingFlow flow = _ending;
                _actor.SetCutsceneDamageBlocked(true);
                var text = new LanguageTextSource(_catalog);
                string notice = string.Join("\n", Array.ConvertAll(_endingTextIds, id => id == 30352 ? text.ReadUi(52008) : text.Read(id, LanguageSettings.Current)));
                // 1114 A2 기획 제안: 검은 화면 가운데 문구, 정해진 시간 뒤 또는 넘기기 입력으로 타이틀.
                bool opened = _textbox.OpenBlackout(notice, _endingNoticeMs,
                    () =>
                    {
                        if (this == null || !isActiveAndEnabled || !ReferenceEquals(_ending, flow)) return;
                        if (_actor != null && !_actor.IsDead && flow.Complete()) _endingReturnPending = true;
                        else { flow.Fail(); _actor?.SetCutsceneDamageBlocked(false); }
                    });
                if (!opened) { flow.Fail(); _actor.SetCutsceneDamageBlocked(false); }
            }
            catch (Exception error) { FailEnding(error); }
        }
        private void ReportCutsceneError(Exception error)
        {
            if (_shrineModalBinding != null) _shrineModalBinding.Report(error, this, "엔딩·컷씬 진행");
            else Debug.LogException(error, this);
        }
        private void FailEnding(Exception error)
        {
            _ending?.Fail(); _endingReturnPending = false;
            _actor?.SetCutsceneDamageBlocked(false);
            ReportCutsceneError(error);
        }
        private void DisconnectCutscenes()
        {
            _ending = null; _endingReturnPending = false; _endingWaitSeconds = 0f;
            _actor?.SetCutsceneDamageBlocked(false);
            if (_afterCutscene != null && _cutscenes != null)
            {
                _cutscenes.Cancel();
                _cutscenes.TryTakeResult(out _);
            }
            _afterCutscene = null;
            _pendingCutsceneId = 0;
            if (_cutsceneRoot != null)
            {
                _cutsceneRoot.SetActive(false);
                Destroy(_cutsceneRoot); _cutsceneRoot = null; _cutscenes = null;
            }
        }
    }
}
