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
        public event Action<PauseItem> ItemHovered;
        public event Action<PauseItem> ItemClicked;
        public event Action<bool> ConfirmHovered;
        public event Action<bool> ConfirmClicked;
        public event Action CloseClicked;

        public static bool ReadConfirmDefaultYes(IReadOnlyDataCatalog data) => data.GetTable<Popup>().Get(_confirmPopup).DefaultButton == 1;

        public void Prepare(IReadOnlyDataCatalog data)
        {
            IReadOnlyDataTable<Language> language = data.GetTable<Language>();
            _title.text = language.Get(30032).KO;
            _resume.text = language.Get(30033).KO;
            _controls.text = language.Get(30034).KO;
            _settings.text = language.Get(30030).KO;
            _toTitle.text = language.Get(30035).KO;
            Popup popup = data.GetTable<Popup>().Get(_confirmPopup);
            _confirmBody.text = language.Get(popup.BodyTextID).KO;
            _confirmYes.text = language.Get(popup.Button1TextID).KO;
            _confirmNo.text = language.Get(popup.Button2TextID).KO;
            _controlsText.text = language.Get(30058).KO + "  방향키 / 왼쪽 스틱\n" + language.Get(30059).KO + "  C / A\n" + language.Get(30060).KO + "  X / X\n" +
                language.Get(30061).KO + "  Space / B\n" + language.Get(30062).KO + "  W·E / 위\n" + language.Get(30022).KO + "  Q / LB\n결박 가르기  Z / Y";
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
            MenuText.SetSelected(_resume, _menu.Selected == PauseItem.Resume);
            MenuText.SetSelected(_controls, _menu.Selected == PauseItem.Controls);
            MenuText.SetSelected(_settings, _menu.Selected == PauseItem.Settings);
            MenuText.SetSelected(_toTitle, _menu.Selected == PauseItem.ReturnToTitle);
            _controlsView.SetActive(_menu.View == PauseView.Controls);
            _confirm.SetActive(_menu.View == PauseView.ConfirmTitle);
            MenuText.SetSelected(_confirmYes, _menu.ConfirmYes);
            MenuText.SetSelected(_confirmNo, !_menu.ConfirmYes);
        }
    }
}
