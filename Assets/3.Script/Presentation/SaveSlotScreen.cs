#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace HumanGhost.Gameplay.Presentation
{
    // 타이틀 조회와 수동 저장이 공유하는 표시·선택 창입니다. 파일과 저장 규칙은 소유하지 않습니다.
    public sealed class SaveSlotScreen : MonoBehaviour
    {
        [Serializable]
        private sealed class Row
        {
            [SerializeField] private Button _button = null;
            [SerializeField] private Text _label = null;
            private UnityAction _click;
            public void Bind(UnityAction click) { _click = click; _button.onClick.AddListener(click); }
            public void Unbind() { if (_click != null) _button.onClick.RemoveListener(_click); _click = null; }
            public void Show(SaveRecord record, string emptyText, bool visible, bool selected, bool enabled)
            {
                _button.gameObject.SetActive(visible);
                _button.interactable = enabled;
                if (visible) _label.text = (selected ? "▶ " : "") + (record == null ? emptyText : FormatDate(record));
            }
        }

        [SerializeField] private Text _heading = null;
        [SerializeField] private Text _status = null;
        [SerializeField] private Button _closeButton = null;
        [SerializeField] private Row[] _rows = Array.Empty<Row>();
        [SerializeField] private GameObject _confirmation = null;
        [SerializeField] private Text _question = null;
        [SerializeField] private Button _yes = null;
        [SerializeField] private Button _no = null;
        [SerializeField] private Text _yesLabel = null;
        [SerializeField] private Text _noLabel = null;
        private IReadOnlyList<SaveSlotEntry> _slots = Array.Empty<SaveSlotEntry>();
        private Action<bool> _answer;
        private bool _acceptSelected, _cancelAnswer, _busy, _allowClose;
        private string _emptyText, _acceptText, _rejectText;
        public int SelectedIndex { get; private set; }
        public event Action<int> SelectionRequested;
        public event Action CloseRequested;

        public void Show(string title, IReadOnlyList<SaveSlotEntry> slots, string emptyText, string status)
        {
            if (slots == null || slots.Count > _rows.Length) throw new InvalidOperationException("저장 기록을 표시할 행이 부족합니다.");
            _slots = slots;
            _emptyText = emptyText;
            _heading.text = title;
            _status.text = status;
            SelectedIndex = 0;
            _busy = false;
            _allowClose = true;
            _closeButton.interactable = true;
            _answer = null;
            _confirmation.SetActive(false);
            gameObject.SetActive(true);
            Render();
        }

        public void Hide() { _answer = null; gameObject.SetActive(false); }
        public void ShowStatus(string message) => _status.text = message;
        public void SetBusy(bool busy, bool allowClose = false)
        {
            _busy = busy;
            _allowClose = allowClose;
            _closeButton.interactable = !busy || allowClose;
            Render();
        }

        public void Confirm(string question, string accept, string reject, bool defaultAccept, bool cancelAnswer, Action<bool> answer)
        {
            _question.text = question;
            _acceptText = accept;
            _rejectText = reject;
            _acceptSelected = defaultAccept;
            _cancelAnswer = cancelAnswer;
            _answer = answer ?? throw new ArgumentNullException(nameof(answer));
            _confirmation.SetActive(true);
            Render();
        }

        public void Move(int direction)
        {
            if (_busy) return;
            if (_answer != null) _acceptSelected = !_acceptSelected;
            else if (_slots.Count > 0) SelectedIndex = (SelectedIndex + Math.Sign(direction) + _slots.Count) % _slots.Count;
            Render();
        }

        public void Submit()
        {
            if (_busy) return;
            if (_answer != null) Answer(_acceptSelected);
            else if (_slots.Count > 0) SelectionRequested?.Invoke(SelectedIndex);
        }

        public void Cancel()
        {
            if (_busy && !_allowClose) return;
            if (_answer != null) Answer(_cancelAnswer);
            else CloseRequested?.Invoke();
        }

        public static string FormatDate(SaveRecord record) => DateTimeOffset.FromUnixTimeMilliseconds(record.SavedAtUnixMs)
            .ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        private void Answer(bool accepted)
        {
            Action<bool> answer = _answer;
            _answer = null;
            _confirmation.SetActive(false);
            Render();
            answer?.Invoke(accepted);
        }

        private void OnEnable()
        {
            _closeButton.onClick.AddListener(Cancel);
            _yes.onClick.AddListener(Accept);
            _no.onClick.AddListener(Reject);
            for (int index = 0; index < _rows.Length; index++)
            {
                int selected = index;
                _rows[index].Bind(() => { if (_busy || _answer != null) return; SelectedIndex = selected; Render(); Submit(); });
            }
        }
        private void OnDisable()
        {
            _closeButton.onClick.RemoveListener(Cancel);
            _yes.onClick.RemoveListener(Accept);
            _no.onClick.RemoveListener(Reject);
            foreach (Row row in _rows) row.Unbind();
        }
        private void Accept() { if (!_busy) Answer(true); }
        private void Reject() { if (!_busy) Answer(false); }
        private void Render()
        {
            for (int index = 0; index < _rows.Length; index++)
                _rows[index].Show(index < _slots.Count ? _slots[index].Record : null, _emptyText,
                    index < _slots.Count, index == SelectedIndex, !_busy && _answer == null);
            if (_answer != null)
            {
                _yesLabel.text = (_acceptSelected ? "▶ " : "") + _acceptText;
                _noLabel.text = (!_acceptSelected ? "▶ " : "") + _rejectText;
            }
        }
    }
}
#endif
