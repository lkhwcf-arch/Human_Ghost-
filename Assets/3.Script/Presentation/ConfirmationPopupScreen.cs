using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace HumanGhost.Gameplay.Presentation
{
    // 공용 패널과 버튼을 조합하며 삭제·이동 규칙은 소유하지 않습니다.
    public class ConfirmationPopupScreen : PopupScreen
    {
        [SerializeField] private Text _message = null;
        [SerializeField] private Text _acceptLabel = null;
        [SerializeField] private Text _cancelLabel = null;
        [SerializeField] private Button _accept = null;
        [SerializeField] private Button _cancel = null;
        [SerializeField] private Button _close = null;
        private bool _busy;
        private bool _cancelEnabled = true;
        private EventSystem _selectionEvents;
        protected override Button PrimaryButton => _accept;
        protected override Button CloseButton => _cancel;
        public event Action Accepted;
        public override void Show(EventSystem events, int defaultButton = 2)
        {
            _selectionEvents = events;
            base.Show(events, defaultButton);
        }
        public void SetText(string message, string accept, string cancel)
        {
            _message.text = message; _acceptLabel.text = accept; _cancelLabel.text = cancel;
        }
        public void SetBusy(bool busy)
        {
            _busy = busy;
            _accept.interactable = !busy;
            _cancel.interactable = _close.interactable = !busy && _cancelEnabled;
        }
        public void SetCancelEnabled(bool enabled)
        {
            _cancelEnabled = enabled;
            SetBusy(_busy);
            if (!enabled && _selectionEvents != null) _selectionEvents.SetSelectedGameObject(_accept.gameObject);
        }
        public override void Move(int direction) { if (!_busy && _cancelEnabled) base.Move(direction); }
        public void Submit()
        {
            if (_busy) return;
            if (_selectionEvents != null && _selectionEvents.currentSelectedGameObject == _close.gameObject) { RequestClose(); return; }
            if (SelectedButton == 1) Accept(); else RequestClose();
        }
        private void Accept() { if (!_busy) Accepted?.Invoke(); }
        public override void RequestClose() { if (!_busy && _cancelEnabled) base.RequestClose(); }
        protected virtual void OnEnable()
        {
            _accept.onClick.AddListener(Accept);
            _cancel.onClick.AddListener(RequestClose);
            _close.onClick.AddListener(RequestClose);
        }
        protected virtual void OnDisable()
        {
            _accept.onClick.RemoveListener(Accept);
            _cancel.onClick.RemoveListener(RequestClose);
            _close.onClick.RemoveListener(RequestClose);
        }
    }
}
