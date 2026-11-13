using System;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Storage;
using UnityEngine;
using UnityEngine.UI;

namespace HumanGhost.Gameplay.Presentation
{
    // 설정 패널(O05·O05_1)입니다. 타이틀과 일시정지가 같은 프리팹을 씁니다. 그림과 조절 표시는 프리팹에서 연결합니다.
    public class SettingsScreen : MonoBehaviour
    {
        private const int _musicText = 30040, _effectsText = 30041, _shakeText = 30042, _onText = 30047, _offText = 30048, _confirmPopup = 15006;
        [SerializeField] private Text _master = null, _masterLeft = null, _masterRight = null;
        [SerializeField, InspectorName("마스터 임시 문구")] private string _masterTitle = "마스터 음량";
        [SerializeField, InspectorName("마스터 문구 ID (0=임시 문구)")] private int _masterTextId = 0;
        [SerializeField] private Text _music = null;
        [SerializeField] private Text _effects = null;
        [SerializeField] private Text _shake = null;
        [SerializeField] private Text _musicLeft = null, _musicRight = null, _effectsLeft = null, _effectsRight = null;
        [SerializeField] private Text _close = null;
        [SerializeField] private GameObject _confirm = null;
        [SerializeField] private Text _confirmBody = null, _confirmYes = null, _confirmNo = null;
        [SerializeField] private Text _layout = null, _windowMode = null;
        [SerializeField] private bool _paperStyle = false;
        [SerializeField] private Slider[] _volumeSliders = Array.Empty<Slider>();
        [SerializeField] private GameObject[] _rowHighlights = Array.Empty<GameObject>();
        private SettingsMenu _menu;
        private IReadOnlyDataTable<Language> _language;
        private Action<GameSettings> _preview;
        private Action<GameSettings> _closed;
        private bool _connected;
        private bool _inputBlocked;
        private int _openedFrame;
        private bool CanInteract => !_inputBlocked && Time.frameCount > _openedFrame;

        public void SetInputBlocked(bool blocked)
        {
            _inputBlocked = blocked;
            if (_menu != null) Render();
        }

        public bool IsOpen => _menu != null;

        public void Open(IReadOnlyDataCatalog data, GameSettings settings, Action<GameSettings> preview, Action<GameSettings> closed, bool allowWindowMode = false)
        {
            if (IsOpen) throw new InvalidOperationException("이미 열린 설정 화면입니다.");
            RequireReferences();
            try
            {
                _language = data.GetTable<Language>();
                Popup popup = data.GetTable<Popup>().Get(_confirmPopup);
                _confirmBody.text = _language.Get(popup.BodyTextID).KO;
                _confirmYes.text = _language.Get(popup.Button1TextID).KO;
                _confirmNo.text = _language.Get(popup.Button2TextID).KO;
                _preview = preview; _closed = closed;
                _openedFrame = Time.frameCount;
                _menu = new SettingsMenu(settings, allowWindowMode);
                _menu.Changed += Render;
                ConnectPointer();
                if (_windowMode != null)
                {
                    _windowMode.gameObject.SetActive(allowWindowMode);
                    if (_paperStyle) _windowMode.transform.parent.gameObject.SetActive(allowWindowMode);
                }
                gameObject.SetActive(true);
                Render();
                ScreenVisibility.RequireVisible(GetComponent<Canvas>());
            }
            catch { Release(); throw; }
        }

        private void RequireReferences()
        {
            foreach (var item in new[] { (_master, "마스터 문구"), (_masterLeft, "마스터 왼쪽 화살표"), (_masterRight, "마스터 오른쪽 화살표"),
                (_music, "배경음 문구"), (_musicLeft, "배경음 왼쪽 화살표"), (_musicRight, "배경음 오른쪽 화살표"),
                (_effects, "효과음 문구"), (_effectsLeft, "효과음 왼쪽 화살표"), (_effectsRight, "효과음 오른쪽 화살표"),
                (_shake, "카메라 흔들림 문구"), (_close, "닫기"), (_confirmBody, "확인창 본문"), (_confirmYes, "확인창 예"), (_confirmNo, "확인창 아니오") })
                if (item.Item1 == null) throw new InvalidOperationException($"설정 화면의 {item.Item2} 참조가 없습니다. SettingsScreen 프리팹 연결을 확인해 주세요.");
            if (_confirm == null) throw new InvalidOperationException("설정 화면의 확인창 오브젝트가 연결되지 않았습니다.");
        }

        private void ConnectPointer()
        {
            // 기존 프리팹은 행을 복제하고 새 시안은 미리 배치한 행을 사용합니다.
            if (_layout == null) _layout = CreateRow("KeyboardLayout", -64f);
            if (_windowMode == null) _windowMode = CreateRow("WindowMode", -128f);
            if (_connected) return;
            _connected = true;
            MenuText.Connect(_windowMode.gameObject, () => Select(SettingsRow.WindowMode),
                () => { Select(SettingsRow.WindowMode); Adjust(1); });
            MenuText.Connect(_layout.gameObject, () => Select(SettingsRow.KeyboardLayout),
                () => { Select(SettingsRow.KeyboardLayout); Adjust(1); });
            ConnectVolume(_master, _masterLeft, _masterRight, SettingsRow.Master);
            ConnectVolume(_music, _musicLeft, _musicRight, SettingsRow.Music);
            ConnectVolume(_effects, _effectsLeft, _effectsRight, SettingsRow.Effects);
            MenuText.Connect(_shake.gameObject, () => Select(SettingsRow.CameraShake), () => { Select(SettingsRow.CameraShake); Adjust(1); });
            MenuText.Connect(_close.gameObject, null, Back);
            MenuText.Connect(_confirmYes.gameObject, null, () => Answer(true));
            MenuText.Connect(_confirmNo.gameObject, null, () => Answer(false));
        }

        private Text CreateRow(string name, float offsetY)
        {
            Text label = Instantiate(_shake, _shake.transform.parent);
            label.name = name;
            label.rectTransform.anchoredPosition += new Vector2(0f, offsetY);
            return label;
        }

        private void ConnectVolume(Text label, Text left, Text right, SettingsRow row)
        {
            MenuText.Connect(label.gameObject, () => Select(row), () => Select(row));
            MenuText.Connect(left.gameObject, null, () => { Select(row); Adjust(-1); });
            MenuText.Connect(right.gameObject, null, () => { Select(row); Adjust(1); });
            Slider slider = VolumeSlider(row);
            if (slider == null) return;
            MenuText.Connect(slider.gameObject, () => Select(row), () => Select(row));
            slider.onValueChanged.AddListener(value =>
            {
                if (!CanInteract || _menu == null || _menu.IsConfirming) { if (_menu != null) Render(); return; }
                Select(row);
                int current = row == SettingsRow.Master ? _menu.Current.Master : row == SettingsRow.Music ? _menu.Current.Music : _menu.Current.Effects;
                Adjust(Mathf.RoundToInt(value) - current);
            });
        }

        private Slider VolumeSlider(SettingsRow row)
        {
            int index = row == SettingsRow.Master ? 0 : row == SettingsRow.Music ? 1 : 2;
            return index < _volumeSliders.Length ? _volumeSliders[index] : null;
        }

        private void Select(SettingsRow row) { if (CanInteract) _menu?.Select(row); }

        public void Move(int direction) { if (CanInteract) _menu?.Move(direction); }

        public void Adjust(int direction)
        {
            if (!CanInteract || _menu == null) return;
            _menu.Adjust(direction);
            _preview?.Invoke(_menu.Current.Clone());
        }

        public void Submit()
        {
            if (!CanInteract || _menu == null) return;
            if (_menu.IsConfirming) Answer(_menu.ConfirmYes);
            else if (_menu.Row == SettingsRow.CameraShake || _menu.Row == SettingsRow.KeyboardLayout || _menu.Row == SettingsRow.WindowMode) Adjust(1);
        }

        public void Back()
        {
            if (!CanInteract || _menu == null) return;
            if (_menu.IsConfirming) { Answer(false); return; }
            if (_menu.RequestClose()) Finish(_menu.Current);
        }

        public void Cancel()
        {
            if (_menu != null) Finish(_menu.Answer(false));
        }

        private void Answer(bool apply)
        {
            if (!CanInteract || _menu == null || !_menu.IsConfirming) return;
            Finish(_menu.Answer(apply));
        }

        private void Finish(GameSettings result)
        {
            Action<GameSettings> closed = _closed;
            Release();
            closed?.Invoke(result);
        }

        private void Release()
        {
            if (_menu != null) _menu.Changed -= Render;
            _menu = null;
            _closed = null;
            _preview = null;
            gameObject.SetActive(false);
        }

        protected virtual void OnDisable() => Cancel();

        private void Render()
        {
            GameSettings value = _menu.Current;
            if (_windowMode != null && _windowMode.gameObject.activeSelf)
            {
                _windowMode.text = "화면 모드  ◀ " + (value.WindowMode == WindowMode.Windowed ? "창모드" : "전체 화면") + " ▶";
                SetSelected(_windowMode, _menu.Row == SettingsRow.WindowMode);
            }
            _layout.text = "키보드 조작  " + (value.KeyboardLayout == KeyboardLayout.Arrows ? "A (방향키 · C/X/Space/Z/Q)" : "B (WASD · K/J/L/U/I)");
            SetSelected(_layout, _menu.Row == SettingsRow.KeyboardLayout);
            RenderVolume(_master, _masterTextId == 0 ? _masterTitle : _language.Get(_masterTextId).KO, value.Master, SettingsRow.Master);
            RenderVolume(_music, _language.Get(_musicText).KO, value.Music, SettingsRow.Music);
            RenderVolume(_effects, _language.Get(_effectsText).KO, value.Effects, SettingsRow.Effects);
            _shake.text = _language.Get(_shakeText).KO + "  " + _language.Get(value.CameraShake ? _onText : _offText).KO;
            SetSelected(_shake, _menu.Row == SettingsRow.CameraShake);
            for (int i = 0; i < _rowHighlights.Length; i++)
                if (_rowHighlights[i] != null) _rowHighlights[i].SetActive(!_menu.IsConfirming && (int)_menu.Row == i);
            _confirm.SetActive(_menu.IsConfirming);
            SetSelected(_confirmYes, _menu.ConfirmYes);
            SetSelected(_confirmNo, !_menu.ConfirmYes);
        }

        private void RenderVolume(Text label, string title, int value, SettingsRow row)
        {
            Slider slider = VolumeSlider(row);
            label.text = slider == null ? title + "  " + Dots(value) : title;
            if (slider != null)
            {
                slider.SetValueWithoutNotify(value);
                slider.interactable = !_inputBlocked && !_menu.IsConfirming;
            }
            SetSelected(label, _menu.Row == row);
        }

        private void SetSelected(Text text, bool selected)
        {
            MenuText.SetSelected(text, selected);
            if (!_paperStyle) return;
            bool button = text == _confirmYes || text == _confirmNo;
            text.color = button ? (selected ? new Color(1f, .85f, .55f) : new Color(.75f, .7f, .63f))
                : selected ? new Color(.42f, .08f, .04f) : new Color(.16f, .10f, .06f);
        }

        private static string Dots(int value)
        {
            var dots = new char[GameSettings.MaxVolume];
            for (int i = 0; i < dots.Length; i++) dots[i] = i < value ? '\u25CF' : '\u25CB';
            return new string(dots);
        }
    }
}
