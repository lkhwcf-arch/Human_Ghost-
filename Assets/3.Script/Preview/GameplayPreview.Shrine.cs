using System;
using HumanGhost.Gameplay.Adapters;
using HumanGhost.Gameplay.Presentation;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 기존 맵 조립자가 화면·입력·모달 수명을 연결합니다. 별도 지갑이나 저장 담당을 만들지 않습니다.
    public sealed partial class GameplayPreview
    {
        [SerializeField, InspectorName("서낭당 화면 프리팹")] private ShrineScreen _shrinePrefab = null;
        [SerializeField, InspectorName("공용 모달 연결")] private ErrorPopupSceneBinding _shrineModalBinding = null;
        private ShrineScreen _shrineScreen;
        private GameObject _shrineRoot;
        private MenuInput _shrineInput;
        private int _shrineClosedFrame = -1;
        private bool _shrineCursorWasVisible;
        private bool _shrineRising;
        private float _shrineRiseRemaining;

        private void OpenShrine()
        {
            if (!CanPause() || _saveFlow == null || !_saveFlow.IsPlayable || _shrineSaving
                || _shrineClosedFrame >= 0 || (_shrineScreen != null && _shrineScreen.IsOpen)) return;
            try
            {
                // 화면을 사용하지 않는 이전 확인 씬은 휴식 저장만 유지합니다.
                if (_shrinePrefab == null) { SaveAtPoint(true); return; }
                if (_shrineModalBinding == null || _input.Source == null)
                    throw new InvalidOperationException("서낭당 화면·공용 모달·입력 연결을 확인해 주세요.");
                SaveAtPoint(true);
                if (_shrineScreen == null)
                {
                    _shrineRoot = new GameObject("ShrineScope", typeof(RectTransform));
                    _shrineRoot.SetActive(false);
                    _shrineRoot.transform.SetParent(transform, false);
                    _shrineScreen = Instantiate(_shrinePrefab, _shrineRoot.transform);
                    _shrineScreen.gameObject.SetActive(false);
                    _shrineInput = new MenuInput(_input.Source, _shrineScreen.Move, _shrineScreen.Submit, _shrineScreen.Back);
                    _shrineRoot.SetActive(true);
                }
                _shrineCursorWasVisible = Cursor.visible;
                _shrineScreen.Open(_catalog, _progress, _actor, CloseShrine, ReportShrineError, _shrineModalBinding.Events);
                _player.NotifyPresentationCue("Pray");
                _shrineModalBinding.SetModalOwner(this, true);
                _shrineInput.Enable();
                Cursor.visible = true;
            }
            catch (Exception error) { DisconnectShrine(); ReportShrineError(error); }
        }
        private void ReportShrineError(Exception error)
        {
            if (_shrineModalBinding != null) _shrineModalBinding.Report(error, this, "서낭당 화면 연결·구매 처리");
            else Debug.LogException(error, this);
        }
        private void BlockShrine(bool blocked)
        {
            bool other = blocked && (_shrineModalBinding == null || _shrineModalBinding.HasOtherModalOwner(this));
            _shrineInput?.SetBlocked(other);
            if (_shrineScreen != null) _shrineScreen.SetBlocked(other);
        }
        private void CloseShrine()
        {
            _shrineInput?.Disable();
            if (_shrineScreen != null) _shrineScreen.Hide();
            Cursor.visible = _shrineCursorWasVisible;
            _shrineRising = true;
            _shrineRiseRemaining = _playerSprite != null ? _playerSprite.BeginSaveRise() : 0f;
            SetGameInputLocked(true);
            _shrineClosedFrame = Time.frameCount;
        }
        private void StepShrineClose()
        {
            if (_shrineClosedFrame >= 0)
            {
                if (Time.frameCount <= _shrineClosedFrame) return;
                _shrineClosedFrame = -1;
                if (_shrineModalBinding != null) _shrineModalBinding.SetModalOwner(this, false);
                SetGameInputLocked(false);
            }
            if (!_shrineRising) return;
            _shrineRiseRemaining = Mathf.Max(0f, _shrineRiseRemaining - Time.deltaTime);
            if (_shrineRiseRemaining > 0f) return;
            _shrineRising = false;
            SetGameInputLocked(false);
        }
        private void DisconnectShrine()
        {
            bool opened = _shrineScreen != null && _shrineScreen.IsOpen;
            _shrineInput?.Dispose(); _shrineInput = null;
            if (_shrineScreen != null) _shrineScreen.Hide();
            if (opened) Cursor.visible = _shrineCursorWasVisible;
            if (_shrineModalBinding != null) _shrineModalBinding.SetModalOwner(this, false);
            _shrineClosedFrame = -1;
            _shrineRising = false; _shrineRiseRemaining = 0f;
            if (_player != null && _player.Actor != null && _player.isActiveAndEnabled) _player.NotifyPresentationCue("ActionExit");
            if (_shrineRoot != null) Destroy(_shrineRoot);
            _shrineRoot = null; _shrineScreen = null;
        }
    }
}
