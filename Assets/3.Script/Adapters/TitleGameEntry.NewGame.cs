using System;
using HumanGhost.Gameplay.Presentation;
using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    // 새 게임의 조작 선택 결과를 소비한 뒤 기존 인트로·게임 진입에 연결합니다.
    public sealed partial class TitleGameEntry
    {
        private int _introCutsceneId;
        private bool _introPending;
        private KeyboardLayoutScreen _layoutScreen;
        private bool _layoutPending, _layoutResultReady;
        private KeyboardLayout? _layoutResult;

        private bool StartNewGame()
        {
            if (_modalBlocked || _menuPending || _introPending || _records == null || _saveStore == null) return false;
            if (KeyboardControls.LastDeviceWasGamepad) return BeginNewGame();
            PrepareMenu();
            if (_layoutScreen == null) _layoutScreen = KeyboardLayoutScreen.Create(_menuRoot.transform, _settingsScreen);
            _layoutScreen.SetBlocked(_modalBlocked);
            _layoutPending = true;
            _layoutScreen.Open(_settings.KeyboardLayout, OnLayoutClosed);
            try
            {
                _settingsInput.SetBlocked(_modalBlocked);
                _settingsInput.Enable();
            }
            catch { _layoutScreen.Cancel(); _layoutPending = false; throw; }
            _menuPending = true;
            _host.enabled = false;
            Cursor.visible = true;
            return true;
        }

        private void OnLayoutClosed(KeyboardLayout? layout)
        {
            _settingsInput?.Disable();
            _layoutResult = layout;
            _layoutResultReady = true;
        }

        private void ConsumeLayoutResult()
        {
            _layoutResultReady = false;
            _layoutPending = false;
            bool started = false;
            try
            {
                if (_layoutResult.HasValue)
                {
                    GameSettings selected = _settings.Clone();
                    selected.SetKeyboardLayout(_layoutResult.Value);
                    if (_settingsApplication.Complete(_settings, selected))
                    {
                        _settings = selected;
                        started = BeginNewGame();
                    }
                }
            }
            catch (Exception error) { _host.ReportError(error, "새 게임 조작 선택 후 시작"); }
            finally
            {
                _layoutResult = null;
                if (!started && isActiveAndEnabled) QueueMenuReturn();
            }
        }

        private bool BeginNewGame()
        {
            if (_data == null || _data.Data == null) return false;
            _introCutsceneId = CutsceneSequenceSource.FindId(_data.Data, CutsceneSequenceSource.IntroKey);
            if (_introCutsceneId == 0) return EnterGame(null);
            if (_modalBlocked || _introPending || _records == null || _saveStore == null) return false;
            if (_cutscenes == null || _data == null || _data.Data == null)
                throw new InvalidOperationException("인트로의 공용 실행 담당과 초기화가 완료된 데이터를 연결해 주세요.");
            if (!_cutscenes.TryPlay(_data.Data, _introCutsceneId, _audio)) return false;
            _introPending = true;
            return true;
        }

        private void LateUpdate()
        {
            if (_menuReturnFrame >= 0 && Time.frameCount > _menuReturnFrame) ReturnFromMenu();
            if (_layoutResultReady) ConsumeLayoutResult();
            if (!_introPending) return;
            CutsceneEndReason reason = CutsceneEndReason.Cancelled;
            if (_cutscenes != null && !_cutscenes.TryTakeResult(out reason)) return;
            _introPending = false;
            try
            {
                if (!reason.AllowsProgress() || !EnterGame(null)) QueueMenuReturn();
                else GameSaveContext.Current.Progress.MarkCutsceneViewed(_introCutsceneId);
            }
            catch (Exception error) { QueueMenuReturn(); _host.ReportError(error, "인트로 종료 후 새 게임 진입"); }
        }
    }
}
