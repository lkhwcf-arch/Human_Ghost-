using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HumanGhost.Gameplay.Adapters;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace HumanGhost.Gameplay.Presentation
{
    // 재생 규칙은 기존 코어가 담당합니다. 이 화면은 표시와 준비 리소스만 소유합니다.
    public class CutsceneScreen : MonoBehaviour, ISkipTarget
    {
        [SerializeField, InspectorName("화면 캔버스")] private Canvas _canvas = null;
        [SerializeField, InspectorName("표시 묶음")] private CanvasGroup _group = null;
        [SerializeField, InspectorName("그림 표시")] private Image _image = null;
        [SerializeField, InspectorName("영상 표시")] private RawImage _videoImage = null;
        [SerializeField, InspectorName("자막 표시")] private Text _subtitle = null;
        [SerializeField, Min(1), InspectorName("영상 준비 제한 시간(밀리초)")] private int _prepareTimeoutMs = 15000;
        private readonly CutscenePlayback _playback = new CutscenePlayback();
        private PreparedAssetStore<Sprite> _images;
        private PreparedAssetStore<VideoClip> _videos;
        private LanguageTextSource _text;
        private LanguageCode _language;
        private VideoPlayer _video;
        private RenderTexture _texture;
        private MillisecondClock _clock;
        private long _shownToken = -1;
        private int _prepareElapsedMs;
        private bool _videoReady, _videoEnded;
        private string _videoError;
        private CutsceneEndReason? _finished;
        private bool _paused;
        public bool IsRunning => _playback.IsRunning;
        public event Action<CutsceneEndReason> Finished;

        public void Connect(LanguageTextSource text, LanguageCode language,
            IAssetSource<Sprite> images = null, IAssetSource<VideoClip> videos = null)
        {
            if (IsRunning) throw new InvalidOperationException("컷씬 재생 중에는 연결을 변경할 수 없습니다.");
            ReleaseDisplay();
            _images?.Dispose();
            _videos?.Dispose();
            _images = new PreparedAssetStore<Sprite>(images ?? new AddressableAssetSource<Sprite>());
            _videos = new PreparedAssetStore<VideoClip>(videos ?? new AddressableAssetSource<VideoClip>());
            _text = text;
            _language = language;
        }

        // 호출자가 준비를 기다립니다. 완료 콜백에서 이 화면을 호출하거나 자동 재생하지 않습니다.
        public Task PrepareAsync(CutsceneSequence sequence, CancellationToken cancellation = default)
        {
            RequireConnected();
            if (sequence == null) throw new ArgumentNullException(nameof(sequence));
            var images = new List<string>();
            var videos = new List<string>();
            foreach (CutsceneStep step in sequence.Steps)
                (step.Media == CutsceneMedia.Image ? images : videos).Add(step.ResourceAddress);
            return Task.WhenAll(_images.PrepareAsync(images, cancellation), _videos.PrepareAsync(videos, cancellation));
        }

        public bool Play(CutsceneSequence sequence)
        {
            RequireConnected();
            if (IsRunning || _finished.HasValue) return false;
            if (sequence == null) throw new ArgumentNullException(nameof(sequence));
            if (_group == null || _image == null || _videoImage == null)
                throw new InvalidOperationException("컷씬의 표시 묶음·그림·영상 표시를 연결해 주세요.");
            if (_prepareTimeoutMs <= 0) throw new InvalidOperationException("영상 준비 제한 시간은 양수여야 합니다.");
            ScreenVisibility.RequireVisible(_canvas);
            // 시작 전에 전체 연결을 확인하여 중간 구간에서 필수 연결 누락으로 갇히지 않게 합니다.
            foreach (CutsceneStep step in sequence.Steps)
            {
                if (!TryGet(step, out UnityEngine.Object asset) || asset == null)
                    throw new InvalidOperationException("컷씬 표시 리소스가 준비되지 않았습니다. 주소: " + step.ResourceAddress);
                if (step.Media == CutsceneMedia.Image ? !(asset is Sprite) : !(asset is VideoClip))
                    throw new InvalidOperationException("컷씬 주소의 타입이 그림(Sprite) 또는 영상(VideoClip) 설정과 다릅니다: " + step.ResourceAddress);
                if (step.TextId != 0)
                {
                    if (_subtitle == null || _text == null) throw new InvalidOperationException("자막이 있는 컷씬에는 자막 표시와 언어 공급자를 연결해 주세요.");
                    _text.Read(step.TextId, _language);
                }
            }
            _finished = null;
            _clock = default;
            _shownToken = -1;
            _group.alpha = 1f;
            _group.blocksRaycasts = true;
            _playback.TryPlay(sequence);
            try { ShowCurrent(); }
            catch (Exception error)
            {
                _playback.Fail(_playback.StepToken, error.Message);
                CompleteIfEnded();
                throw;
            }
            return true;
        }

        public void Advance() => _playback.Advance();
        public bool CanSkip(SkipScope scope) => isActiveAndEnabled && !_paused && IsRunning &&
            (scope == SkipScope.Cutscene || scope == SkipScope.Video && _playback.Current.Media == CutsceneMedia.Video);
        public bool TrySkip(SkipScope scope)
        {
            if (!CanSkip(scope)) return false;
            bool skipped = scope == SkipScope.Cutscene ? _playback.Skip() : _playback.SkipVideo();
            // 다음 표시는 기존 갱신에 맡기고 이전 영상은 즉시 정리합니다.
            if (skipped) ReleaseDisplay();
            return skipped;
        }
        public void Cancel() { _playback.Cancel(); CompleteIfEnded(); }
        public void SetPaused(bool paused)
        {
            if (_paused == paused) return;
            _paused = paused;
            _clock = default;
            if (_video != null && _playback.State == CutscenePlaybackState.Playing)
            {
                if (paused) _video.Pause(); else _video.Play();
            }
        }

        public void Disconnect()
        {
            Cancel();
            ReleaseDisplay();
            _images?.Dispose(); _images = null;
            _videos?.Dispose(); _videos = null;
            _text = null;
            _paused = false;
            if (_group != null) { _group.alpha = 0f; _group.blocksRaycasts = false; }
        }

        protected virtual void OnEnable() => _playback.Finished += OnFinished;
        private void OnFinished(CutsceneEndReason reason) => _finished = reason;
        protected virtual void OnDisable()
        {
            Disconnect();
            _playback.Finished -= OnFinished;
        }

        protected virtual void Update()
        {
            if (_paused || (!IsRunning && !_finished.HasValue)) return;
            int elapsed = _clock.Advance(Time.unscaledDeltaTime);
            try
            {
                if (_shownToken != _playback.StepToken) { ShowCurrent(); elapsed = 0; }
                if (_videoError != null) throw new InvalidOperationException(_videoError);
                if (_video != null && _playback.State == CutscenePlaybackState.Preparing)
                {
                    _prepareElapsedMs += Math.Min(elapsed, _prepareTimeoutMs - _prepareElapsedMs);
                    if (_videoReady) { _videoReady = false; _video.Play(); _playback.MarkReady(_shownToken); elapsed = 0; }
                    else if (_prepareElapsedMs == _prepareTimeoutMs) throw new InvalidOperationException("컷씬 영상 준비 제한 시간을 초과했습니다.");
                }
                if (_videoEnded) { _videoEnded = false; _playback.ReportVideoEnded(_shownToken); elapsed = 0; }
                _playback.Tick(elapsed);
                ShowCurrent();
            }
            catch (Exception error)
            {
                ReleaseDisplay();
                _playback.Fail(_playback.StepToken, error.Message);
                Debug.LogException(new InvalidOperationException("컷씬 표시 중 오류가 발생했습니다.", error), this);
            }
            CompleteIfEnded();
        }

        private void ShowCurrent()
        {
            if (!IsRunning || _shownToken == _playback.StepToken) return;
            ReleaseDisplay();
            _shownToken = _playback.StepToken;
            CutsceneStep step = _playback.Current;
            if (!TryGet(step, out UnityEngine.Object asset) || asset == null)
                throw new InvalidOperationException("재생할 컷씬 리소스가 제거됐습니다: " + step.ResourceAddress);
            if (_subtitle != null) _subtitle.text = step.TextId == 0 ? "" : _text.Read(step.TextId, _language);
            if (step.Media == CutsceneMedia.Image)
            {
                _image.sprite = (Sprite)asset;
                _image.enabled = true;
                _playback.MarkReady(_shownToken);
                return;
            }
            var clip = (VideoClip)asset;
            _texture = new RenderTexture(Math.Max(1, (int)clip.width), Math.Max(1, (int)clip.height), 0);
            _texture.Create();
            var root = new GameObject("CutsceneVideo");
            root.SetActive(false);
            root.transform.SetParent(transform, false);
            _video = root.AddComponent<VideoPlayer>();
            _video.playOnAwake = false;
            _video.isLooping = false;
            _video.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            _video.renderMode = VideoRenderMode.RenderTexture;
            _video.targetTexture = _texture;
            _video.clip = clip;
            _video.prepareCompleted += OnVideoReady;
            _video.loopPointReached += OnVideoEnded;
            _video.errorReceived += OnVideoError;
            _videoImage.texture = _texture;
            _videoImage.enabled = true;
            root.SetActive(true);
            _video.Prepare();
        }

        // Unity 완료 이벤트에서는 코어 명령을 호출하지 않고 다음 실행 흐름에 결과만 전달합니다.
        private void OnVideoReady(VideoPlayer source) { if (source == _video) _videoReady = true; }
        private void OnVideoEnded(VideoPlayer source) { if (source == _video) _videoEnded = true; }
        private void OnVideoError(VideoPlayer source, string message) { if (source == _video) _videoError = message; }
        private void CompleteIfEnded()
        {
            if (!_finished.HasValue) return;
            var reason = _finished.Value;
            _finished = null;
            ReleaseDisplay();
            if (_group != null) { _group.alpha = 0f; _group.blocksRaycasts = false; }
            Finished?.Invoke(reason);
        }
        private void ReleaseDisplay()
        {
            if (_image != null) { _image.enabled = false; _image.sprite = null; }
            if (_videoImage != null) { _videoImage.enabled = false; _videoImage.texture = null; }
            if (_subtitle != null) _subtitle.text = "";
            if (_video != null)
            {
                _video.prepareCompleted -= OnVideoReady;
                _video.loopPointReached -= OnVideoEnded;
                _video.errorReceived -= OnVideoError;
                _video.Stop();
                _video.clip = null;
                _video.targetTexture = null;
                Destroy(_video.gameObject);
                _video = null;
            }
            if (_texture != null) { _texture.Release(); Destroy(_texture); _texture = null; }
            _videoReady = _videoEnded = false;
            _videoError = null;
            _prepareElapsedMs = 0;
        }
        private bool TryGet(CutsceneStep step, out UnityEngine.Object asset)
        {
            if (step.Media == CutsceneMedia.Image)
            {
                bool found = _images.TryGet(step.ResourceAddress, out Sprite sprite);
                asset = sprite;
                return found;
            }
            bool loaded = _videos.TryGet(step.ResourceAddress, out VideoClip video);
            asset = video;
            return loaded;
        }
        private void RequireConnected()
        {
            if (this == null || !isActiveAndEnabled || (_images == null || _videos == null))
                throw new InvalidOperationException("컷씬 화면을 활성화하고 데이터를 연결해 주세요.");
        }
    }
}
