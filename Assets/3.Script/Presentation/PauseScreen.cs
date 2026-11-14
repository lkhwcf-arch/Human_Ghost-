using HumanGhost.Gameplay.Adapters;
using System;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Storage;
using UnityEngine;
using UnityEngine.UI;

namespace HumanGhost.Gameplay.Presentation
{
    // 일시정지 화면(O04)입니다. 표시와 입력 위치 전달만 맡고 흐름은 PauseMenu·호스트가 맡습니다.
    public sealed class PauseScreen : MonoBehaviour
    {
        private const int _confirmPopup = 15004;
        [SerializeField] private Text _title = null;
        [SerializeField] private Text _resume = null, _controls = null, _settings = null, _toTitle = null;
        [SerializeField] private GameObject _controlsView = null;
        [SerializeField] private Text _controlsText = null;
        [SerializeField] private GameObject _confirm = null;
        [SerializeField] private Text _confirmBody = null, _confirmYes = null, _confirmNo = null;
        [SerializeField] private Text _close = null;
        private PauseMenu _menu;
        private bool _connected;
        private LanguageTextSource _text;
        public event Action<PauseItem> ItemHovered;
        public event Action<PauseItem> ItemClicked;
        public event Action<bool> ConfirmHovered;
        public event Action<bool> ConfirmClicked;
        public event Action CloseClicked;

        public static bool ReadConfirmDefaultYes(IReadOnlyDataCatalog data) => data.GetTable<Popup>().Get(_confirmPopup).DefaultButton == 1;

        public void Prepare(IReadOnlyDataCatalog data)
        {
            IReadOnlyDataTable<Language> language = data.GetTable<Language>();
            _text = new LanguageTextSource(data);
            _title.text = _text.ReadUi(51101);
            _resume.text = _text.ReadUi(51102);
            _controls.text = _text.ReadUi(51103);
            _settings.text = _text.ReadUi(51104);
            _toTitle.text = _text.ReadUi(51105);
            Popup popup = data.GetTable<Popup>().Get(_confirmPopup);
            _confirmBody.text = LanguageTextSource.Select(language.Get(popup.BodyTextID));
            _confirmYes.text = LanguageTextSource.Select(language.Get(popup.Button1TextID));
            _confirmNo.text = LanguageTextSource.Select(language.Get(popup.Button2TextID));
            _close.text = _text.ReadUi(51505);
            RefreshControls();
            if (_connected) return;
            _connected = true;
            Connect(_resume, PauseItem.Resume); Connect(_controls, PauseItem.Controls); Connect(_settings, PauseItem.Settings); Connect(_toTitle, PauseItem.ReturnToTitle);
            MenuText.Connect(_confirmYes.gameObject, () => ConfirmHovered?.Invoke(true), () => ConfirmClicked?.Invoke(true));
            MenuText.Connect(_confirmNo.gameObject, () => ConfirmHovered?.Invoke(false), () => ConfirmClicked?.Invoke(false));
            MenuText.Connect(_close.gameObject, null, () => CloseClicked?.Invoke());
        }

        private void Connect(Text text, PauseItem item) => MenuText.Connect(text.gameObject, () => ItemHovered?.Invoke(item), () => ItemClicked?.Invoke(item));

        public void Show(PauseMenu menu)
        {
            _menu = menu;
            _menu.Changed += Render;
            gameObject.SetActive(true);
            Render();
        }

        public void Hide()
        {
            if (_menu != null) _menu.Changed -= Render;
            _menu = null;
            gameObject.SetActive(false);
        }

        public void Render()
        {
            if (_menu == null) return;
            RefreshControls();
            MenuText.SetSelected(_resume, _menu.Selected == PauseItem.Resume);
            MenuText.SetSelected(_controls, _menu.Selected == PauseItem.Controls);
            MenuText.SetSelected(_settings, _menu.Selected == PauseItem.Settings);
            MenuText.SetSelected(_toTitle, _menu.Selected == PauseItem.ReturnToTitle);
            _controlsView.SetActive(_menu.View == PauseView.Controls);
            _confirm.SetActive(_menu.View == PauseView.ConfirmTitle);
            MenuText.SetSelected(_confirmYes, _menu.ConfirmYes);
            MenuText.SetSelected(_confirmNo, !_menu.ConfirmYes);
        }

        private void RefreshControls()
        {
            if (_text == null) return;
            bool wasd = Adapters.KeyboardControls.Layout == KeyboardLayout.Wasd;
            string Ui(int id) => _text.ReadUi(id);
            _controlsText.text = Ui(51301) + "\n" + Ui(51302) + " / " + Ui(51303) + " / " + Ui(51304) + "\n" +
                Ui(51305) + (wasd ? "  WASD" : "  " + Ui(51315)) + " / " + Ui(51316) + "\n" +
                Ui(51306) + (wasd ? "  K" : "  C") + " / A\n" +
                Ui(51307) + (wasd ? "  J" : "  X") + " / X\n" +
                Ui(51308) + (wasd ? "  L" : "  Space") + " / B\n" +
                Ui(51309) + "  F / " + Ui(51317) + "\n" + Ui(51310) + (wasd ? "  I" : "  Q") + " / LB\n" +
                Ui(51311) + (wasd ? "  U" : "  Z") + " / Y\n" +
                Ui(51312) + (wasd ? "  S+K" : "  ↓+C") + " / ↓+A\n" + Ui(51313) + "  Esc / Start";
        }
    }
}
