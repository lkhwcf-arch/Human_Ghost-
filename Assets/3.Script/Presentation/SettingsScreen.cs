using System;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Storage;
using UnityEngine;
using UnityEngine.UI;

namespace HumanGhost.Gameplay.Presentation
{
    // 설정 패널(O05·O05_1)입니다. 타이틀과 일시정지가 같은 프리팹을 씁니다. 그림은 공급 뒤 교체합니다.
    public sealed class SettingsScreen : MonoBehaviour
    {
        private const int _musicText = 30040, _effectsText = 30041, _shakeText = 30042, _onText = 30047, _offText = 30048, _confirmPopup = 15006;
        [SerializeField] private Text _music = null;
        [SerializeField] private Text _effects = null;
        [SerializeField] private Text _shake = null;
        [SerializeField] private Text _musicLeft = null, _musicRight = null, _effectsLeft = null, _effectsRight = null;
        [SerializeField] private Text _close = null;
        [SerializeField] private GameObject _confirm = null;
        [SerializeField] private Text _confirmBody = null, _confirmYes = null, _confirmNo = null;
        private SettingsMenu _menu;
        private IReadOnlyDataTable<Language> _language;
        private Action<GameSettings> _preview;
        private Action<GameSettings> _closed;
        private bool _connected;

        public bool IsOpen => _menu != null;

        public void Open(IReadOnlyDataCatalog data, GameSettings settings, Action<GameSettings> preview, Action<GameSettings> closed)
        {
            _language = data.GetTable<Language>();
            Popup popup = data.GetTable<Popup>().Get(_confirmPopup);
            _confirmBody.text = _language.Get(popup.BodyTextID).KO;
            _confirmYes.text = _language.Get(popup.Button1TextID).KO;
            _confirmNo.text = _language.Get(popup.Button2TextID).KO;
            _preview = preview; _closed = closed;
            _menu = new SettingsMenu(settings);
            _menu.Changed += Render;
            ConnectPointer();
            gameObject.SetActive(true);
            Render();
        }

        private void ConnectPointer()
        {
            if (_connected) return;
            _connected = true;
            MenuText.Connect(_music.gameObject, () => _menu?.Select(SettingsRow.Music), () => _menu?.Select(SettingsRow.Music));
            MenuText.Connect(_effects.gameObject, () => _menu?.Select(SettingsRow.Effects), () => _menu?.Select(SettingsRow.Effects));
            MenuText.Connect(_shake.gameObject, () => _menu?.Select(SettingsRow.CameraShake), () => { _menu?.Select(SettingsRow.CameraShake); Adjust(1); });
            MenuText.Connect(_musicLeft.gameObject, null, () => { _menu?.Select(SettingsRow.Music); Adjust(-1); });
            MenuText.Connect(_musicRight.gameObject, null, () => { _menu?.Select(SettingsRow.Music); Adjust(1); });
            MenuText.Connect(_effectsLeft.gameObject, null, () => { _menu?.Select(SettingsRow.Effects); Adjust(-1); });
            MenuText.Connect(_effectsRight.gameObject, null, () => { _menu?.Select(SettingsRow.Effects); Adjust(1); });
            MenuText.Connect(_close.gameObject, null, Back);
            MenuText.Connect(_confirmYes.gameObject, null, () => Answer(true));
            MenuText.Connect(_confirmNo.gameObject, null, () => Answer(false));
        }

        public void Move(int direction) => _menu?.Move(direction);

        public void Adjust(int direction)
        {
            if (_menu == null) return;
            _menu.Adjust(direction);
            _preview?.Invoke(_menu.Current);
        }

        public void Submit()
        {
            if (_menu == null) return;
            if (_menu.IsConfirming) Answer(_menu.ConfirmYes);
            else if (_menu.Row == SettingsRow.CameraShake) Adjust(1);
        }

        public void Back()
        {
            if (_menu == null) return;
            if (_menu.IsConfirming) { Answer(false); return; }
            if (_menu.RequestClose()) Finish(_menu.Current);
        }

        private void Answer(bool apply)
        {
            if (_menu == null || !_menu.IsConfirming) return;
            Finish(_menu.Answer(apply));
        }

        private void Finish(GameSettings result)
        {
            _menu.Changed -= Render;
            _menu = null;
            gameObject.SetActive(false);
            Action<GameSettings> closed = _closed;
            _closed = null;
            closed?.Invoke(result);
        }

        private void Render()
        {
            GameSettings value = _menu.Current;
            _music.text = _language.Get(_musicText).KO + "  " + Dots(value.Music);
            _effects.text = _language.Get(_effectsText).KO + "  " + Dots(value.Effects);
            _shake.text = _language.Get(_shakeText).KO + "  " + _language.Get(value.CameraShake ? _onText : _offText).KO;
            MenuText.SetSelected(_music, _menu.Row == SettingsRow.Music);
            MenuText.SetSelected(_effects, _menu.Row == SettingsRow.Effects);
            MenuText.SetSelected(_shake, _menu.Row == SettingsRow.CameraShake);
            _confirm.SetActive(_menu.IsConfirming);
            MenuText.SetSelected(_confirmYes, _menu.ConfirmYes);
            MenuText.SetSelected(_confirmNo, !_menu.ConfirmYes);
        }

        private static string Dots(int value)
        {
            var dots = new char[GameSettings.MaxVolume];
            for (int i = 0; i < dots.Length; i++) dots[i] = i < value ? '\u25CF' : '\u25CB';
            return new string(dots);
        }
    }
}
