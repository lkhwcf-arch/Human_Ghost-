using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace HumanGhost.Gameplay.Presentation
{
    // 메뉴 뒤의 반복 영상만 소유합니다. 준비 전·실패 시 아래의 기존 정지 배경을 유지합니다.
    public sealed class TitleVideoBackground : MonoBehaviour
    {
        [SerializeField] private VideoPlayer _video = null;
        [SerializeField] private RawImage _image = null;
        public bool IsReady { get; private set; }
        public event Action Ready;

        private void OnEnable()
        {
            IsReady = false;
            if (_image != null) { _image.enabled = false; _image.raycastTarget = false; }
            if (_video == null || _image == null || _video.clip == null)
            {
                OnError(_video, "타이틀 영상·출력 화면을 연결해 주세요.");
                return;
            }
            _video.playOnAwake = false; _video.isLooping = true;
            _video.renderMode = VideoRenderMode.APIOnly;
            _video.audioOutputMode = VideoAudioOutputMode.None;
            _video.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            _video.prepareCompleted += OnPrepared;
            _video.errorReceived += OnError;
            _video.Prepare();
        }
        private void OnPrepared(VideoPlayer source)
        {
            if (!isActiveAndEnabled || source != _video) return;
            _image.texture = source.texture;
            _image.enabled = true;
            source.Play();
            IsReady = true;
            Ready?.Invoke();
        }
        private void OnError(VideoPlayer source, string message)
        {
            if (!isActiveAndEnabled) return;
            if (_image != null) _image.enabled = false;
            if (source != null) source.Stop();
            Debug.LogError("타이틀 배경 영상: " + message, this);
            IsReady = true;
            Ready?.Invoke();
        }
        private void OnDisable()
        {
            IsReady = false;
            if (_video != null)
            {
                _video.prepareCompleted -= OnPrepared;
                _video.errorReceived -= OnError;
                _video.Stop();
            }
            if (_image != null) { _image.enabled = false; _image.texture = null; }
        }
    }
}
