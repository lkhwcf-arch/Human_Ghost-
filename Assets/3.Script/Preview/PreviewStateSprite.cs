using System;
using System.Collections.Generic;
using HumanGhost.Data.Definitions;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // CSV StateKey에 연결한 클립을 표시합니다. 재생은 판정·상태 전환을 변경하지 않습니다.
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
        [Serializable]
        private sealed class StateAnimation
        {
            [SerializeField] private StateKey _state = StateKey.Idle;
            [SerializeField] private AnimationClip _clip = null;
            public StateKey State => _state;
            public AnimationClip Clip => _clip;
        }
        [SerializeField, InspectorName("상태 애니메이션")] private StateAnimation[] _animations = Array.Empty<StateAnimation>();
        private AnimationClip _animation;
        private float _elapsed;
        private float _feetY;
        private StateKey _state;
        private bool _hasState;
        private Sprite _selected;
        private readonly Dictionary<StateKey, AnimationClip> _clipsByState = new Dictionary<StateKey, AnimationClip>();
        private readonly Dictionary<StateKey, Sprite> _framesByState = new Dictionary<StateKey, Sprite>();
        private bool _lookupReady;
        protected virtual void OnValidate() => _lookupReady = false;
        private void PrepareLookup()
        {
            if (_lookupReady) return;
            _clipsByState.Clear(); _framesByState.Clear();
            foreach (var entry in _animations)
                if (entry != null && entry.Clip != null && !_clipsByState.ContainsKey(entry.State)) _clipsByState.Add(entry.State, entry.Clip);
            foreach (var entry in _frames)
                if (entry != null && entry.Sprite != null && !_framesByState.ContainsKey(entry.State)) _framesByState.Add(entry.State, entry.Sprite);
            _lookupReady = true;
        }
        protected AnimationClip CurrentAnimation => _animation;
        protected void RestartAnimation() => _hasState = false;
        protected SpriteRenderer Renderer => _renderer;
        protected Sprite Fallback => _fallback;
        protected abstract bool TryGetState(out StateKey state);
        protected virtual bool AlignFeet => true;
        protected virtual float AnimationDelta => Time.deltaTime;
        protected virtual float Duration(StateKey state) => 0f;
        protected virtual AnimationClip SelectAnimation(StateKey state)
        {
            PrepareLookup();
            return _clipsByState.TryGetValue(state, out var clip) ? clip : null;
        }

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
            PrepareLookup();
            return _framesByState.TryGetValue(state, out var sprite) ? sprite : null;
        }

        protected virtual void LateUpdate()
        {
            if (_renderer == null || !TryGetState(out StateKey state)) return;
            AnimationClip animation = SelectAnimation(state);
            bool changed = !_hasState || state != _state || animation != _animation;
            if (changed)
            {
                _hasState = true; _state = state; _animation = animation; _elapsed = 0f;
                _selected = SelectFrame(state);
            }
            if (_animation != null)
            {
                float duration = Duration(state);
                float time = duration > 0f ? _elapsed / duration * _animation.length : _elapsed;
                time = _animation.isLooping && duration <= 0f ? Mathf.Repeat(time, _animation.length) : Mathf.Min(time, _animation.length);
                _animation.SampleAnimation(_renderer.gameObject, time);
            }
            else if (_selected != null && _renderer.sprite != _selected) _renderer.sprite = _selected;
            // 셀 내부의 동작을 보존합니다. 프레임마다 발을 강제로 당겨 구르기·사망을 흔들지 않습니다.
            if (changed && AlignFeet && _fallback != null && _renderer.sprite != null)
            {
                Vector3 position = _renderer.transform.localPosition;
                position.y = _feetY - PreviewPresentation.VisibleBottom(_renderer.sprite) * _renderer.transform.localScale.y;
                _renderer.transform.localPosition = position;
            }
            _elapsed += AnimationDelta;
            UpdateFacing();
        }
    }
}
