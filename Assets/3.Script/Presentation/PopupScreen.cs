using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HumanGhost.Gameplay.Presentation
{
    // 팝업 공통 표시·선택·입력 복귀를 상속으로 재사용합니다.
    public abstract class PopupScreen : MonoBehaviour
    {
        private EventSystem _events;
        private GameObject _previousSelection;
        protected abstract Button PrimaryButton { get; }
        protected abstract Button CloseButton { get; }
        public int SelectedButton => _events != null && _events.currentSelectedGameObject == CloseButton.gameObject ? 2 : 1;
        public event Action CloseRequested;

        public virtual void Show(EventSystem events, int defaultButton = 1)
        {
            if (PrimaryButton == null || CloseButton == null) throw new InvalidOperationException("팝업 버튼을 연결해 주세요.");
            if (events == null || !events.isActiveAndEnabled) throw new InvalidOperationException("활성화된 UI 입력 시스템을 연결해 주세요.");
            _events = events;
            _previousSelection = events.currentSelectedGameObject;
            gameObject.SetActive(true);
            ScreenVisibility.RequireVisible(GetComponent<Canvas>());
            events.SetSelectedGameObject((defaultButton == 2 ? CloseButton : PrimaryButton).gameObject);
        }

        public virtual void Move(int direction)
        {
            if (direction == 0 || _events == null) return;
            _events.SetSelectedGameObject((SelectedButton == 1 ? CloseButton : PrimaryButton).gameObject);
        }

        public virtual void RequestClose() => CloseRequested?.Invoke();

        public virtual void Hide()
        {
            gameObject.SetActive(false);
            if (_events != null) _events.SetSelectedGameObject(_previousSelection != null && _previousSelection.activeInHierarchy ? _previousSelection : null);
            _events = null;
            _previousSelection = null;
        }
    }
}
