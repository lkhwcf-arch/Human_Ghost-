using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace HumanGhost.Gameplay.Presentation
{
    // 메뉴 뒤의 반복 영상만 소유합니다. 준비 전·실패 시 아래의 기존 정지 배경을 유지합니다.
    public class TitleVideoBackground : MonoBehaviour
    {
        [SerializeField] private VideoPlayer _video = null;
        [SerializeField] private RawImage _image = null;
        [Tooltip("StreamingAssets 아래 영상의 상대 경로입니다. 파일을 같은 이름으로 교체하면 연결은 유지됩니다.")]
        [SerializeField] private string _videoFile = "";
        public bool IsReady { get; private set; }
        public event Action Ready;

        private void OnEnable()
        {
            IsReady = false;
            if (_image != null) { _image.enabled = false; _image.raycastTarget = false; }
            if (_video == null || _image == null || string.IsNullOrWhiteSpace(_videoFile))
            {
                OnError(_video, "타이틀 영상·출력 화면을 연결해 주세요.");
                return;
            }
            string path = Path.Combine(Application.streamingAssetsPath, _videoFile);
            if (!File.Exists(path))
            {
                OnError(_video, "타이틀 영상 파일을 찾을 수 없습니다: " + path);
                return;
            }
            // 원본 파일을 직접 읽어 빌드 에디터의 영상 가져오기 지원 여부와 분리합니다.
            _video.source = VideoSource.Url;
            _video.url = new Uri(Path.GetFullPath(path)).AbsoluteUri;
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
