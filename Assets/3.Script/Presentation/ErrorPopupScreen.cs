using System;
using UnityEngine;
using UnityEngine.UI;

namespace HumanGhost.Gameplay.Presentation
{
    // 오류 표시와 복사 요청만 추가하고 공통 표시 수명은 부모에게 둡니다.
    public class ErrorPopupScreen : PopupScreen
    {
        [SerializeField] private Text _message = null;
        [SerializeField] private Text _copyLabel = null;
        [SerializeField] private Text _closeLabel = null;
        [SerializeField] private Text _status = null;
        [SerializeField] private Button _close = null;
        [SerializeField] private Button _copy = null;
        protected override Button PrimaryButton => _copy;
        protected override Button CloseButton => _close;
        public event Action CopyRequested;
        public string Content => _message.text;

        public virtual void SetText(string message, string copy, string close)
        {
            if (_message == null || _copyLabel == null || _closeLabel == null || _status == null)
                throw new InvalidOperationException("에러 팝업 문구와 상태 표시를 연결해 주세요.");
            _message.text = message;
            _copyLabel.text = copy;
            _closeLabel.text = close;
        }

        public virtual void ShowStatus(string message) => _status.text = message;
        public virtual void RequestCopy() => CopyRequested?.Invoke();
        protected virtual void OnEnable()
        {
            _close.onClick.AddListener(RequestClose);
            _copy.onClick.AddListener(RequestCopy);
        }
        protected virtual void OnDisable()
        {
            _close.onClick.RemoveListener(RequestClose);
            _copy.onClick.RemoveListener(RequestCopy);
        }
    }
}
