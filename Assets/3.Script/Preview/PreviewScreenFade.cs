using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace HumanGhost.Gameplay.Preview
{
    // 확인 장면 전용 먹색 암전입니다. 씬을 넘어 유지되고 끝나면 스스로 제거합니다.
    public sealed class PreviewScreenFade : MonoBehaviour
    {
        private static readonly Color _ink = new Color(0.04f, 0.04f, 0.05f, 1f);
        private Image _image;
        public static bool IsActive { get; private set; }

        // onBlack은 완전히 어두워졌을 때 한 번 호출합니다.
        public static void Run(float fadeOut, float hold, float fadeIn, Action onBlack)
        {
            if (IsActive) return;
            var root = new GameObject("PreviewScreenFade");
            DontDestroyOnLoad(root);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2000;
            var view = new GameObject("Ink");
            view.transform.SetParent(root.transform, false);
            var rect = view.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            var fade = root.AddComponent<PreviewScreenFade>();
            fade._image = view.AddComponent<Image>();
            fade._image.raycastTarget = false;
            IsActive = true;
            fade.StartCoroutine(fade.Play(fadeOut, hold, fadeIn, onBlack));
        }

        private IEnumerator Play(float fadeOut, float hold, float fadeIn, Action onBlack)
        {
            yield return Tween(0f, 1f, fadeOut);
            onBlack?.Invoke();
            yield return new WaitForSecondsRealtime(hold);
            yield return Tween(1f, 0f, fadeIn);
            Destroy(gameObject);
        }

        private IEnumerator Tween(float from, float to, float seconds)
        {
            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
            {
                SetAlpha(Mathf.Lerp(from, to, elapsed / seconds));
                yield return null;
            }
            SetAlpha(to);
        }

        private void SetAlpha(float alpha)
        {
            Color color = _ink; color.a = alpha;
            _image.color = color;
        }

        private void OnDestroy() => IsActive = false;
    }
}
