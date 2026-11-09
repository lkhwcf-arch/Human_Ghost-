using System;
using HumanGhost.Data.Storage;
using HumanGhost.Gameplay.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HumanGhost.Gameplay.Adapters
{
    // 일시정지 흐름(O04·O04_1·O05_1)을 조립합니다. 열 수 있는지와 타이틀 이동은 조립자가 전달합니다.
    public sealed class PauseMenuHost : MonoBehaviour, IModalInputTarget
    {
        [SerializeField] private PauseScreen _screen = null;
        [SerializeField] private SettingsScreen _settingsScreen = null;
        [SerializeField] private InputActionAsset _input = null;
        private bool _modalBlocked;
        public void SetModalBlocked(bool blocked)
        {
            _modalBlocked = blocked;
            _menuInput?.SetBlocked(blocked);
            if (_settingsScreen != null) _settingsScreen.SetInputBlocked(blocked);
        }
        private MenuInput _menuInput;
        private PauseMenu _menu;
        private IReadOnlyDataCatalog _data;
        private GameSpeedController _speed;
        private GameAudio _audio;
        private GameSettings _settings;
        private SettingsApplication _settingsApplication;
        private Func<bool> _canOpen;
        private Action<bool> _setGameInputLocked;
        private Action _returnToTitle;
        private Action<GameSettings> _settingsChanged;
        private bool _unlockNextFrame;
        public bool IsOpen => _menu != null;

        public void Connect(IReadOnlyDataCatalog data, GameSpeedController speed, GameAudio audio, GameSettings settings,
            Func<bool> canOpen, Action<bool> setGameInputLocked, Action returnToTitle, Action<GameSettings> settingsChanged)
        {
            if (_data != null) throw new InvalidOperationException("이미 연결된 일시정지 메뉴입니다. 먼저 Disconnect를 호출해 주세요.");
            if (_screen == null || _settingsScreen == null || _input == null || speed == null || canOpen == null || setGameInputLocked == null)
                throw new InvalidOperationException("일시정지의 화면·설정 화면·입력·게임 속도·호출 계약을 연결해 주세요.");
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _speed = speed; _audio = audio; _settings = settings;
            _canOpen = canOpen; _setGameInputLocked = setGameInputLocked; _returnToTitle = returnToTitle; _settingsChanged = settingsChanged;
            _settingsApplication = new SettingsApplication(Preview, GameSettingsStore.Save, error => Debug.LogException(error, this), KeyboardControls.SetLayout);
            try
            {
                _screen.Prepare(data);
                _screen.ItemHovered += OnHover; _screen.ItemClicked += OnClick;
                _screen.ConfirmHovered += OnConfirmHover; _screen.ConfirmClicked += OnConfirmClick; _screen.CloseClicked += OnBack;
                _screen.gameObject.SetActive(false);
                _settingsScreen.gameObject.SetActive(false);
                _settingsScreen.SetInputBlocked(_modalBlocked);
                _menuInput = new MenuInput(_input, OnMove, OnSubmit, OnBack, OnSide);
                _menuInput.SetBlocked(_modalBlocked);
            }
            catch { Disconnect(); throw; }
        }

        public void Disconnect()
        {
            if (IsOpen) Teardown();
            if (_data == null) return;
            if (_screen != null)
            {
                _screen.ItemHovered -= OnHover; _screen.ItemClicked -= OnClick;
                _screen.ConfirmHovered -= OnConfirmHover; _screen.ConfirmClicked -= OnConfirmClick; _screen.CloseClicked -= OnBack;
            }
            _menuInput?.Dispose();
            _menuInput = null; _data = null;
            _canOpen = null; _setGameInputLocked = null; _returnToTitle = null; _settingsChanged = null;
            _settingsApplication = null; _speed = null; _audio = null;
            _unlockNextFrame = false;
        }

        private void OnEnable() => InputSystem.onDeviceChange += OnDeviceChange;
        private void OnDisable()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
            if (IsOpen) Teardown();
            _menuInput?.Disable();
            if (_unlockNextFrame) _setGameInputLocked?.Invoke(false);
            _unlockNextFrame = false;
        }
        private void OnApplicationFocus(bool focused) { if (!focused) TryOpen(); }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (device is Gamepad && (change == InputDeviceChange.Disconnected || change == InputDeviceChange.Removed)) TryOpen();
        }

        private void TryOpen()
        {
            if (isActiveAndEnabled && !_modalBlocked && _data != null && !IsOpen && _canOpen()) Open();
        }

        private void Update()
        {
            if (_data == null || _modalBlocked) return;
            if (_unlockNextFrame) { _unlockNextFrame = false; _setGameInputLocked(false); }
            Keyboard keyboard = Keyboard.current;
            Gamepad pad = Gamepad.current;
            bool escape = keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
            bool start = pad != null && pad.startButton.wasPressedThisFrame;
            if (!IsOpen) { if (escape || start) TryOpen(); return; }
            // 패드 Start는 어느 단계에서든 통째로 닫습니다. 바꾼 설정이 있으면 확인을 먼저 받습니다.
            if (start)
            {
                if (_settingsScreen.IsOpen) _settingsScreen.Back();
                if (!_settingsScreen.IsOpen) Close(true);
            }
        }

        private void Open()
        {
            _menu = new PauseMenu(PauseScreen.ReadConfirmDefaultYes(_data));
            _speed.State.SetPaused(true);
            _audio?.SetPaused(true);
            _setGameInputLocked(true);
            Cursor.visible = true;
            _screen.Show(_menu);
            _menuInput.Enable();
        }

        private void Close(bool discardFrameInput)
        {
            _screen.Hide();
            _menu = null;
            _menuInput.Disable();
            _speed.State.SetPaused(false);
            _audio?.SetPaused(false);
            Cursor.visible = false;
            // 닫는 프레임의 입력이 게임으로 새지 않게 다음 프레임에 풉니다.
            if (discardFrameInput) _unlockNextFrame = true; else _setGameInputLocked(false);
        }

        // 씬 종료·Play 중지 때는 화면이 먼저 파괴됐을 수 있어 살아 있는 대상만 정리합니다. 게임 속도는 컨트롤러가 복구합니다.
        private void Teardown()
        {
            if (_settingsScreen != null) _settingsScreen.Cancel();
            if (_screen != null) _screen.Hide();
            _menu = null;
            _menuInput?.Disable();
            if (_speed != null) _speed.State.SetPaused(false);
            if (_audio != null) _audio.SetPaused(false);
            _setGameInputLocked?.Invoke(false);
        }

        private void OnMove(int direction) { if (_modalBlocked) return; if (_settingsScreen.IsOpen) _settingsScreen.Move(direction); else _menu?.Move(direction); }
        private void OnSide(int direction) { if (_modalBlocked) return; if (_settingsScreen.IsOpen) _settingsScreen.Adjust(direction); }

        private void OnSubmit()
        {
            if (_modalBlocked) return;
            if (_settingsScreen.IsOpen) { _settingsScreen.Submit(); return; }
            if (_menu != null) Handle(_menu.Submit());
        }

        private void OnBack()
        {
            if (_modalBlocked) return;
            if (_settingsScreen.IsOpen) { _settingsScreen.Back(); return; }
            if (_menu != null) Handle(_menu.Back());
        }

        private void Handle(PauseRequest request)
        {
            switch (request)
            {
                case PauseRequest.Close: Close(true); break;
                case PauseRequest.OpenSettings:
                    _settingsScreen.Open(_data, _settings, Preview, OnSettingsClosed);
                    break;
                case PauseRequest.ReturnToTitle:
                    Close(false);
                    _setGameInputLocked(true);
                    _returnToTitle();
                    break;
            }
        }

        private void Preview(GameSettings value) => _settingsChanged?.Invoke(value);

        private void OnSettingsClosed(GameSettings result)
        {
            if (_settingsApplication.Complete(_settings, result)) _settings = result;
        }

        private void OnHover(PauseItem item) { if (!_modalBlocked) _menu?.Select(item); }
        private void OnClick(PauseItem item) { if (_modalBlocked || _menu == null || _settingsScreen.IsOpen) return; _menu.Select(item); Handle(_menu.Submit()); }
        private void OnConfirmHover(bool yes) { if (!_modalBlocked) _menu?.SelectConfirm(yes); }
        private void OnConfirmClick(bool yes) { if (_modalBlocked || _menu == null) return; _menu.SelectConfirm(yes); Handle(_menu.Submit()); }

        private void OnDestroy() => Disconnect();
    }
}
