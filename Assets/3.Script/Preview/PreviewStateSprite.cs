using System;
using HumanGhost.Data.Definitions;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 고정 상태 프레임의 표시·발 정렬을 공유합니다. 런타임 관측과 방향만 자식이 제공합니다.
    public abstract class PreviewStateSprite : MonoBehaviour
    {
        [Serializable]
        private sealed class StateFrame
        {
            [SerializeField] private StateKey _state = StateKey.Idle;
            [SerializeField] private Sprite _sprite = null;
            public StateKey State => _state;
            public Sprite Sprite => _sprite;
        }
        [SerializeField, InspectorName("표시 렌더러")] private SpriteRenderer _renderer = null;
        [SerializeField, InspectorName("기본 프레임")] private Sprite _fallback = null;
        [SerializeField, InspectorName("상태별 프레임")] private StateFrame[] _frames = Array.Empty<StateFrame>();
        private float _feetY;
        private StateKey _state;
        private bool _hasState;
        private Sprite _selected;
        protected SpriteRenderer Renderer => _renderer;
        protected Sprite Fallback => _fallback;
        protected abstract bool TryGetState(out StateKey state);

        protected virtual void Awake()
        {
            if (_renderer != null && _fallback != null)
                _feetY = _renderer.transform.localPosition.y + PreviewPresentation.VisibleBottom(_fallback) * _renderer.transform.localScale.y;
        }
        protected virtual void OnEnable() => _hasState = false;
        protected virtual void UpdateFacing() { }
        protected virtual Sprite SelectFrame(StateKey state) => FindFrame(state) ?? _fallback;

        protected Sprite FindFrame(StateKey state)
        {
            foreach (StateFrame frame in _frames)
                if (frame != null && frame.State == state && frame.Sprite != null) return frame.Sprite;
            return null;
        }

        protected virtual void LateUpdate()
        {
            if (_renderer == null || !TryGetState(out StateKey state)) return;
            if (!_hasState || state != _state)
            {
                _hasState = true; _state = state;
                _selected = SelectFrame(state);
            }
            if (_selected != null && _renderer.sprite != _selected)
            {
                _renderer.sprite = _selected;
                if (_fallback != null)
                {
                    Vector3 position = _renderer.transform.localPosition;
                    position.y = _feetY - PreviewPresentation.VisibleBottom(_selected) * _renderer.transform.localScale.y;
                    _renderer.transform.localPosition = position;
                }
            }
            UpdateFacing();
        }
    }
}
