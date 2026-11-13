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
        [SerializeField, Min(0.01f)] private float _fadeIn = 0.5f;
        [SerializeField, Min(0f)] private float _hold = 2f;
        [SerializeField, Min(0.01f)] private float _fadeOut = 0.5f;
        private float _elapsed;

        public void Show(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("맵 이름이 필요합니다.", nameof(text));
            if (_label == null || _group == null || _fadeIn <= 0f || _hold < 0f || _fadeOut <= 0f)
                throw new InvalidOperationException("맵 이름 표시·시간을 연결해 주세요.");
            _label.text = text;
            _elapsed = 0f;
            _group.alpha = 0f;
            _group.blocksRaycasts = _group.interactable = false;
            gameObject.SetActive(true);
        }

        private void Update() => Advance(Time.unscaledDeltaTime);
        private void Advance(float seconds)
        {
            _elapsed += seconds;
            _group.alpha = _elapsed < _fadeIn ? Mathf.Clamp01(_elapsed / _fadeIn)
                : 1f - Mathf.Clamp01((_elapsed - _fadeIn - _hold) / _fadeOut);
            if (_elapsed >= _fadeIn + _hold + _fadeOut) gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            _elapsed = 0f;
            if (_group != null) _group.alpha = 0f;
        }
    }
}
