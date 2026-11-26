using System;
using HumanGhost.Gameplay.Presentation;
using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    // 새 게임 요청을 기존 인트로·게임 진입에 연결합니다.
    public sealed partial class TitleGameEntry
    {
        private int _introCutsceneId;
        private bool _introPending;

        private bool StartNewGame()
        {
            if (_modalBlocked || _menuPending || _introPending || _records == null || _saveStore == null || LoadingScreen.IsActive ||
                _data == null || _data.Data == null) return false;
            _introCutsceneId = CutsceneSequenceSource.FindId(_data.Data, CutsceneSequenceSource.IntroKey);
            if (_introCutsceneId == 0) return EnterGame(null);
            PrepareCutscenes();
            if (!_cutscenes.TryPlay(_data.Data, _introCutsceneId, _audio, SkipScope.Cutscene)) return false;
            _introPending = true;
            return true;
        }

        private void PrepareCutscenes()
        {
            if (_cutscenes != null) return;
            if (_cutscenePrefab == null || _host == null || _host.Modals == null)
                throw new InvalidOperationException("인트로의 공용 컷씬 프리팹과 타이틀 모달 연결을 준비해 주세요.");
            _cutscenes = CutsceneHost.Create(_cutscenePrefab, transform, _host.Modals, out _);
        }

        private void LateUpdate()
        {
            if (_menuReturnFrame >= 0 && Time.frameCount > _menuReturnFrame) ReturnFromMenu();
            if (!_introPending) return;
            CutsceneEndReason reason = CutsceneEndReason.Cancelled;
            if (_cutscenes != null && !_cutscenes.TryTakeResult(out reason)) return;
            _introPending = false;
            try
            {
                // 인트로 뒤에는 타이틀을 다시 보이지 않고 로딩 화면이 인게임 진입 직전까지 덮습니다.
                if (!reason.AllowsProgress() || !EnterGame(null, covered: true)) QueueMenuReturn();
                else GameSaveContext.Current.Progress.MarkCutsceneViewed(_introCutsceneId);
            }
            catch (Exception error) { QueueMenuReturn(); _host.ReportError(error, "인트로 종료 후 새 게임 진입"); }
        }
    }
}
