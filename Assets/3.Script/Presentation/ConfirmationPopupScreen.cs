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
        private bool _noticeMode;
        private Vector2 _normalAcceptPosition;
        private int _normalMessageFont, _normalAcceptFont;
        private Vector3 _normalPanelScale;
        private EventSystem _selectionEvents;
        protected override Button PrimaryButton => _accept;
        protected override Button CloseButton => _noticeMode ? _close : _cancel;
        public event Action Accepted;
        public override void Show(EventSystem events, int defaultButton = 2)
        {
            _selectionEvents = events;
            base.Show(events, defaultButton);
        }
        public void SetText(string message, string accept, string cancel)
        {
            if (_noticeMode)
            {
                ((RectTransform)_accept.transform).anchoredPosition = _normalAcceptPosition;
                _message.fontSize = _normalMessageFont; _acceptLabel.fontSize = _normalAcceptFont;
                _message.transform.parent.localScale = _normalPanelScale;
            }
            _noticeMode = false;
            _cancel.gameObject.SetActive(true);
            _message.text = message; _acceptLabel.text = accept; _cancelLabel.text = cancel;
        }
        public void SetNotice(string message, string dismiss)
        {
            var accept = (RectTransform)_accept.transform;
            if (!_noticeMode)
            {
                _normalAcceptPosition = accept.anchoredPosition; _normalMessageFont = _message.fontSize; _normalAcceptFont = _acceptLabel.fontSize;
                _normalPanelScale = _message.transform.parent.localScale;
            }
            accept.anchoredPosition = new Vector2(0f, accept.anchoredPosition.y);
            _noticeMode = true;
            _cancelEnabled = true;
            _cancel.gameObject.SetActive(false);
            _message.text = message; _acceptLabel.text = dismiss;
            SetBusy(false);
        }
        public void FitNoticeWithin(Vector2 availableSize)
        {
            var panel = _message.transform.parent as RectTransform;
            if (panel == null || panel.rect.width <= 0f || panel.rect.height <= 0f || availableSize.x <= 0f || availableSize.y <= 0f)
                throw new InvalidOperationException("안내 팝업 패널과 기준 화면 크기를 확인해 주세요.");
            float scale = Mathf.Min(1f, availableSize.x / panel.rect.width, availableSize.y / panel.rect.height);
            panel.localScale = Vector3.one * scale;
            if (_noticeMode)
            { _message.fontSize = Mathf.CeilToInt(26f / scale); _acceptLabel.fontSize = Mathf.CeilToInt(22f / scale); }
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
        public override void Move(int direction) { if (!_busy && _cancelEnabled && !_noticeMode) base.Move(direction); }
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
