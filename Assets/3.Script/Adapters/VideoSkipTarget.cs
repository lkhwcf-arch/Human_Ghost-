using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Video;

namespace HumanGhost.Gameplay.Adapters
{
    // 컷씬 밖의 독립 영상용입니다. 자연 종료는 영상 소유자의 기존 연결을 사용합니다.
    public class VideoSkipTarget : MonoBehaviour, ISkipTarget
    {
        [SerializeField, InspectorName("영상 플레이어")] private VideoPlayer _player = null;
        [SerializeField, InspectorName("스킵 완료 후 연결")] private UnityEvent _skipped = new UnityEvent();
        public event Action Skipped;
        private bool _ended;
        public event Action SkipAvailabilityChanged;
        public void NotifyPlaybackChanged() => SkipAvailabilityChanged?.Invoke();
        private void OnVideoStarted(VideoPlayer source) { _ended = false; NotifyPlaybackChanged(); }
        private void OnVideoEnded(VideoPlayer source) { _ended = !source.isLooping; NotifyPlaybackChanged(); }
        private void OnVideoError(VideoPlayer source, string message) { _ended = true; NotifyPlaybackChanged(); }
        protected virtual void OnEnable()
        {
            _ended = false;
            if (_player == null) return;
            _player.started += OnVideoStarted;
            _player.loopPointReached += OnVideoEnded;
            _player.errorReceived += OnVideoError;
            NotifyPlaybackChanged();
        }
        protected virtual void OnDisable()
        {
            if (_player != null)
            {
                _player.started -= OnVideoStarted;
                _player.loopPointReached -= OnVideoEnded;
                _player.errorReceived -= OnVideoError;
            }
            NotifyPlaybackChanged();
        }
        public bool CanSkip(SkipScope scope) => scope == SkipScope.Video && isActiveAndEnabled && !_ended && _player != null && _player.isPlaying;
        public bool TrySkip(SkipScope scope)
        {
            if (!CanSkip(scope)) return false;
            _player.Stop();
            NotifyPlaybackChanged();
            Skipped?.Invoke();
            _skipped.Invoke();
            return true;
        }
    }
}
