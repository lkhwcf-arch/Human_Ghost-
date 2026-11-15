using System;
using UnityEngine;
using UnityEngine.UI;

namespace HumanGhost.Gameplay.Presentation
{
    // 맵 진입 문구의 표시만 담당합니다. 이름과 표시 시점은 호출자가 전달합니다.
    public sealed class StageNameBanner : MonoBehaviour
    {
        [SerializeField] private Text _label = null;
        [SerializeField] private CanvasGroup _group = null;
        [SerializeField, Min(1)] private int _fadeInMilliseconds = 500;
        [SerializeField, Min(0)] private int _holdMilliseconds = 2000;
        [SerializeField, Min(1)] private int _fadeOutMilliseconds = 500;
        private int _elapsedMilliseconds;
        private MillisecondClock _clock;

        public void Show(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("맵 이름이 필요합니다.", nameof(text));
            if (_label == null || _group == null || _fadeInMilliseconds <= 0 || _holdMilliseconds < 0 || _fadeOutMilliseconds <= 0)
                throw new InvalidOperationException("맵 이름 표시·시간을 연결해 주세요.");
            if ((long)_fadeInMilliseconds + _holdMilliseconds + _fadeOutMilliseconds > int.MaxValue)
                throw new InvalidOperationException("맵 이름 표시 총시간이 정수 ms 범위를 넘었습니다.");
            _label.text = text;
            _elapsedMilliseconds = 0; _clock = default;
            _group.alpha = 0f;
            _group.blocksRaycasts = _group.interactable = false;
            gameObject.SetActive(true);
        }

        private void Update() => Advance(_clock.Advance(Time.unscaledDeltaTime));
        private void Advance(int milliseconds)
        {
            long total = (long)_fadeInMilliseconds + _holdMilliseconds + _fadeOutMilliseconds;
            _elapsedMilliseconds += Math.Min(milliseconds, int.MaxValue - _elapsedMilliseconds);
            _group.alpha = _elapsedMilliseconds < _fadeInMilliseconds ? Mathf.Clamp01(_elapsedMilliseconds / (float)_fadeInMilliseconds)
                : 1f - Mathf.Clamp01(((long)_elapsedMilliseconds - _fadeInMilliseconds - _holdMilliseconds) / (float)_fadeOutMilliseconds);
            if (_elapsedMilliseconds >= total) gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            _elapsedMilliseconds = 0; _clock = default;
            if (_group != null) _group.alpha = 0f;
        }
    }
}
