using System;
using UnityEngine;
using UnityEngine.UI;

namespace HumanGhost.Gameplay.Presentation
{
    public sealed class KeyboardLayoutScreen : MonoBehaviour
    {
        private Text _arrows, _wasd;
        private Action<KeyboardLayout?> _completed;
        private KeyboardLayout _selection;
        private int _openedFrame;
        private bool _blocked;
        private bool CanInteract => !_blocked && isActiveAndEnabled && _completed != null && Time.frameCount > _openedFrame;
        public void SetBlocked(bool blocked) => _blocked = blocked;

        public static KeyboardLayoutScreen Create(Transform parent, SettingsScreen template)
        {
            Text exemplar = template != null ? template.GetComponentInChildren<Text>(true) : null;
            if (exemplar == null || exemplar.font == null) throw new InvalidOperationException("조작 선택의 글꼴 참조를 연결해 주세요.");
            var root = new GameObject("KeyboardLayoutScreen", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            try
            {
                root.SetActive(false);
                root.transform.SetParent(parent, false);
                var screenRect = root.GetComponent<RectTransform>();
                screenRect.anchorMin = Vector2.zero; screenRect.anchorMax = Vector2.one;
                screenRect.offsetMin = Vector2.zero; screenRect.offsetMax = Vector2.zero;
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.overrideSorting = true;
                canvas.sortingOrder = 220;
                var scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                var screen = root.AddComponent<KeyboardLayoutScreen>();
                var background = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
                background.transform.SetParent(root.transform, false);
                var rect = background.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
                background.GetComponent<Image>().color = new Color(.04f, .04f, .04f, .97f);
                screen.Label("Title", "조작 방식을 고르세요", 180, exemplar, false);
                screen._arrows = screen.Label("Arrows", "A (방향키)    점프 C · 공격 X · 구르기 Space · 기술 Z · 약초 Q", 50, exemplar, true);
                screen._wasd = screen.Label("Wasd", "B (WASD)    점프 K · 공격 J · 구르기 L · 기술 U · 약초 I", -40, exemplar, true);
                Text cancel = screen.Label("Cancel", "뒤로", -190, exemplar, true);
                MenuText.Connect(screen._arrows.gameObject, () => screen.Hover(KeyboardLayout.Arrows), () => screen.Choose(KeyboardLayout.Arrows));
                MenuText.Connect(screen._wasd.gameObject, () => screen.Hover(KeyboardLayout.Wasd), () => screen.Choose(KeyboardLayout.Wasd));
                MenuText.Connect(cancel.gameObject, null, screen.Back);
                return screen;
            }
            catch { UnityEngine.Object.Destroy(root); throw; }
        }

        private Text Label(string name, string value, float y, Text template, bool interactive)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Text));
            root.transform.SetParent(transform, false);
            var text = root.GetComponent<Text>();
            text.font = template.font; text.fontSize = 30; text.alignment = TextAnchor.MiddleCenter;
            text.text = value; text.color = Color.white; text.raycastTarget = interactive;
            text.rectTransform.sizeDelta = new Vector2(1500, 70);
            text.rectTransform.anchoredPosition = new Vector2(0, y);
            return text;
        }

        public void Open(KeyboardLayout layout, Action<KeyboardLayout?> completed)
        {
            if (_completed != null) throw new InvalidOperationException("이미 열린 조작 선택 화면입니다.");
            if (completed == null) throw new ArgumentNullException(nameof(completed));
            _completed = completed; _openedFrame = Time.frameCount;
            gameObject.SetActive(true); Select(layout);
        }
        private void Select(KeyboardLayout layout)
        {
            _selection = layout;
            MenuText.SetSelected(_arrows, layout == KeyboardLayout.Arrows);
            MenuText.SetSelected(_wasd, layout == KeyboardLayout.Wasd);
        }
        private void Hover(KeyboardLayout layout) { if (CanInteract) Select(layout); }
        private void Choose(KeyboardLayout layout) { if (CanInteract) { Select(layout); Submit(); } }
        public void Move(int direction) { if (CanInteract && direction != 0) Select(_selection == KeyboardLayout.Arrows ? KeyboardLayout.Wasd : KeyboardLayout.Arrows); }
        public void Submit() { if (CanInteract) Complete(_selection); }
        public void Back() { if (CanInteract) Complete(null); }
        public void Cancel() { _completed = null; gameObject.SetActive(false); }
        private void OnDisable()
        {
            Action<KeyboardLayout?> completed = _completed;
            _completed = null;
            completed?.Invoke(null);
        }
        private void Complete(KeyboardLayout? layout)
        {
            Action<KeyboardLayout?> completed = _completed;
            _completed = null; gameObject.SetActive(false); completed?.Invoke(layout);
        }
    }
}
