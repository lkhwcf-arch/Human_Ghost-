using System;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Storage;
using UnityEngine;
using UnityEngine.UI;

namespace HumanGhost.Gameplay.Presentation
{
    // 화면 참조와 표시만 담당합니다. 메뉴 선택·저장·씬 이동 규칙은 포함하지 않습니다.
    public sealed class TitleScreen : MonoBehaviour
    {
        [Serializable]
        private sealed class Entry
        {
            [SerializeField] private TitleAction _action = default;
            [SerializeField] private int _textId = 0;
            [SerializeField] private Text _label = null;
            [SerializeField] private GameObject _cursor = null;
            [SerializeField, InspectorName("글자 이미지(없으면 문구 표시)")] private Image _labelImage = null;
            public TitleAction Action => _action;
            public GameObject Root => _label.transform.parent.gameObject;
            public void SetText(IReadOnlyDataTable<Language> language)
            {
                _label.text = language.Get(_textId).KO;
                // 이미지 항목도 문구를 유지해 이미지를 빼면 언어 표시로 돌아갑니다.
                bool useImage = _labelImage != null && _labelImage.sprite != null;
                _label.enabled = !useImage;
                _label.raycastTarget = !useImage;
                if (_labelImage != null) _labelImage.enabled = useImage;
            }
            public void Show(TitleMenu menu, Color selected, Color normal)
            {
                bool visible = false;
                foreach (TitleAction item in menu.Items) if (item == _action) { visible = true; break; }
                Root.SetActive(visible);
                if (_cursor != null) _cursor.SetActive(false);
                Color color = menu.Selected == _action ? selected : normal;
                _label.color = color;
                if (_labelImage != null) _labelImage.color = color;
            }
        }

        [SerializeField] private GameObject _menuRoot = null;
        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();
        [SerializeField, InspectorName("선택된 메뉴 글자 색")] private Color _entrySelected = Color.white;
        [SerializeField, InspectorName("선택되지 않은 메뉴 글자 색")] private Color _entryNormal = new Color(0.55f, 0.53f, 0.5f, 0.65f);
        [SerializeField, InspectorName("새 게임 확인 Popup ID"), Min(1)] private int _newGamePopupId = 15005;
        [SerializeField] private GameObject _confirmation = null;
        [SerializeField] private Text _question = null;
        [SerializeField] private Text _answerHint = null;
        [SerializeField] private Text _status = null;
        [SerializeField] private Text _yesLabel = null;
        [SerializeField] private Text _noLabel = null;
        [SerializeField] private Text _closeLabel = null;
        [SerializeField, InspectorName("확인 창 고른 답 색")] private Color _answerSelected = Color.white;
        [SerializeField, InspectorName("확인 창 안 고른 답 색")] private Color _answerNormal = new Color(0.55f, 0.53f, 0.5f);
        private TitleMenu _menu;
        private bool _pointerConnected;

        // 표시 소비자는 입력 위치만 알립니다. 메뉴 상태 변경은 호스트가 합니다.
        public event Action<TitleAction> EntryHovered;
        public event Action<TitleAction> EntryClicked;
        public event Action<bool> ConfirmHovered;
        public event Action<bool> ConfirmClicked;
        public event Action CloseClicked;

        // 새 게임 확인 창의 처음 커서입니다. 메뉴 생성 전에 호출합니다.
        public bool ReadConfirmDefaultYes(IReadOnlyDataCatalog data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            var popup = data.GetTable<Popup>().Get(_newGamePopupId);
            if (popup.Button3TextID != 0 || popup.CancelButton != 2 || (popup.DefaultButton != 1 && popup.DefaultButton != 2))
                throw new InvalidOperationException("타이틀 확인창은 두 버튼·취소 둘째 버튼·기본 첫째 또는 둘째 버튼 설정을 사용합니다.");
            return popup.DefaultButton == 1;
        }

        public void Connect(TitleMenu menu, IReadOnlyDataCatalog data)
        {
            Disconnect();
            if (menu == null) throw new ArgumentNullException(nameof(menu));
            if (data == null) throw new ArgumentNullException(nameof(data));
            var language = data.GetTable<Language>();
            var popup = data.GetTable<Popup>().Get(_newGamePopupId);
            ReadConfirmDefaultYes(data);
            foreach (Entry entry in _entries) entry.SetText(language);
            _question.text = language.Get(popup.BodyTextID).KO;
            _yesLabel.text = language.Get(popup.Button1TextID).KO;
            _noLabel.text = language.Get(popup.Button2TextID).KO;
            _answerHint.text = $"↑↓ 고르기 · Enter / C / 패드 A 결정 · Esc / 패드 B: {_noLabel.text}";
            ConnectPointer();
            _menu = menu;
            _menuRoot.SetActive(true);
            Apply();
            if (isActiveAndEnabled) _menu.Changed += Render;
        }

        public void Disconnect()
        {
            if (_menu != null) _menu.Changed -= Render;
            _menu = null;
            if (_menuRoot != null) _menuRoot.SetActive(false);
        }

        private void ConnectPointer()
        {
            if (_pointerConnected) return;
            _pointerConnected = true;
            foreach (Entry entry in _entries)
            {
                TitleAction action = entry.Action;
                MenuText.Connect(entry.Root, () => EntryHovered?.Invoke(action), () => EntryClicked?.Invoke(action));
            }
            MenuText.Connect(_yesLabel.gameObject, () => ConfirmHovered?.Invoke(true), () => ConfirmClicked?.Invoke(true));
            MenuText.Connect(_noLabel.gameObject, () => ConfirmHovered?.Invoke(false), () => ConfirmClicked?.Invoke(false));
            MenuText.Connect(_closeLabel.gameObject, null, () => CloseClicked?.Invoke());
        }

        public void ShowStatus(string message) => _status.text = message;
        private void OnEnable() { if (_menu != null) { _menu.Changed += Render; Render(); } }
        private void OnDisable() { if (_menu != null) _menu.Changed -= Render; }
        private void OnDestroy() => Disconnect();

        private void Render()
        {
            try
            {
                Apply();
            }
            catch (Exception error)
            {
                Disconnect();
                Debug.LogError($"타이틀 화면 표시에 실패했습니다.\n{error.Message}", this);
                enabled = false;
            }
        }

        private void Apply()
        {
            foreach (Entry entry in _entries) entry.Show(_menu, _entrySelected, _entryNormal);
            _confirmation.SetActive(_menu.State == TitleMenuState.ConfirmingNewGame);
            _yesLabel.color = _menu.ConfirmYes ? _answerSelected : _answerNormal;
            _noLabel.color = _menu.ConfirmYes ? _answerNormal : _answerSelected;
        }
    }
}
