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
            public void SetText(IReadOnlyDataTable<Language> language) => _label.text = language.Get(_textId).KO;
            public void Show(TitleMenu menu)
            {
                bool visible = false;
                foreach (TitleAction item in menu.Items) if (item == _action) { visible = true; break; }
                _label.transform.parent.gameObject.SetActive(visible);
                _cursor.SetActive(visible && menu.Selected == _action);
            }
        }

        [SerializeField] private GameObject _menuRoot = null;
        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();
        [SerializeField] private GameObject _confirmation = null;
        [SerializeField] private Text _question = null;
        [SerializeField] private Text _answerHint = null;
        [SerializeField] private Text _status = null;
        private TitleMenu _menu;

        public void Connect(TitleMenu menu, IReadOnlyDataCatalog data)
        {
            Disconnect();
            if (menu == null) throw new ArgumentNullException(nameof(menu));
            var language = data.GetTable<Language>();
            foreach (Entry entry in _entries) entry.SetText(language);
            _question.text = language.Get(30037).KO;
            _answerHint.text = $"Enter / C / 패드 A: {language.Get(30038).KO}    Esc / 패드 B: {language.Get(30039).KO}";
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
            foreach (Entry entry in _entries) entry.Show(_menu);
            _confirmation.SetActive(_menu.State == TitleMenuState.ConfirmingNewGame);
        }
    }
}
