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
        private static PreviewScreenFade _active;
        public static bool IsActive => _active != null;

        // onBlack은 완전히 어두워졌을 때 한 번 호출합니다.
        public static void Run(int fadeOutMilliseconds, int holdMilliseconds, int fadeInMilliseconds, Action onBlack)
        {
            if (IsActive) return;
            if (fadeOutMilliseconds < 0 || holdMilliseconds < 0 || fadeInMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(fadeOutMilliseconds));
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
            _active = fade;
            fade.StartCoroutine(fade.Play(fadeOutMilliseconds, holdMilliseconds, fadeInMilliseconds, onBlack));
        }

        private IEnumerator Play(int fadeOut, int hold, int fadeIn, Action onBlack)
        {
            yield return Tween(0f, 1f, fadeOut);
            bool failed = false;
            try { onBlack?.Invoke(); }
            catch (Exception error)
            {
                Debug.LogException(error, this);
                Destroy(gameObject);
                failed = true;
            }
            if (failed) yield break;
            var holdClock = new MillisecondClock();
            for (int elapsed = 0; elapsed < hold; elapsed += Math.Min(holdClock.AdvanceFrame(Time.unscaledDeltaTime), hold - elapsed))
                yield return null;
            yield return Tween(1f, 0f, fadeIn);
            Destroy(gameObject);
        }

        private IEnumerator Tween(float from, float to, int milliseconds)
        {
            var clock = new MillisecondClock();
            for (int elapsed = 0; elapsed < milliseconds; elapsed += Math.Min(clock.AdvanceFrame(Time.unscaledDeltaTime), milliseconds - elapsed))
            {
                SetAlpha(Mathf.Lerp(from, to, elapsed / (float)milliseconds));
                yield return null;
            }
            SetAlpha(to);
        }

        private void SetAlpha(float alpha)
        {
            Color color = _ink; color.a = alpha;
            _image.color = color;
        }

        private void OnDestroy() { if (_active == this) _active = null; }
        private void OnDisable()
        {
            StopAllCoroutines();
            if (_active == this) _active = null;
            Destroy(gameObject);
        }
    }
}
