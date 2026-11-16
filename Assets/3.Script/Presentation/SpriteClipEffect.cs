using System;
using UnityEngine;

namespace HumanGhost.Gameplay.Presentation
{
    // 호출자가 시간과 위치를 공급하는 단발 표시입니다. 판정·사운드·게임 속도를 소유하지 않습니다.
    public sealed class SpriteClipEffect : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _renderer = null;
        [SerializeField] private AnimationClip _clip = null;
        [SerializeField] private AnimationClip[] _variants = Array.Empty<AnimationClip>();
        private readonly System.Random _random = new System.Random();
        private AnimationClip _playingClip;
        private int _elapsedMilliseconds, _durationMilliseconds;
        private MillisecondClock _clock;
        public bool IsPlaying => gameObject.activeSelf;
        public void Play(Vector3 position)
        {
            _playingClip = _variants.Length == 0 ? _clip : _variants[_random.Next(_variants.Length)];
            if (_renderer == null || _playingClip == null) throw new InvalidOperationException("단발 이펙트의 렌더러·클립을 연결하세요.");
            transform.position = position; _elapsedMilliseconds = 0; _clock = default;
            _durationMilliseconds = MillisecondClock.ToMilliseconds(_playingClip.length);
            gameObject.SetActive(true); _playingClip.SampleAnimation(_renderer.gameObject, 0f);
        }
        public void Step(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (!IsPlaying) return;
            _elapsedMilliseconds += Math.Min(_clock.AdvanceFrame(seconds), _durationMilliseconds - _elapsedMilliseconds);
            if (_elapsedMilliseconds >= _durationMilliseconds) { gameObject.SetActive(false); return; }
            _playingClip.SampleAnimation(_renderer.gameObject, _elapsedMilliseconds / 1000f);
        }
    }
}
