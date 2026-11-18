using HumanGhost.Gameplay.Adapters;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Storage;
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace HumanGhost.Gameplay.Preview
{
    // 시작 맵의 안내만 소유합니다. 이름은 공통 언어 데이터, 키는 현재 입력 설정에서 읽습니다.
    // 플레이어가 판넬 앞을 처음 지나가면 현재 키 배치(A 방향키·B WASD)에 맞는 안내 영상을 정해진 횟수만큼 재생하고 숨깁니다.
    public class StartMapControlsView : MonoBehaviour
    {
        private const string _keyPrefix = "StartGuide.";
        // 영상 끝 도달 횟수로 셉니다. 실수 시간을 누적하지 않습니다.
        private const int _playCount = 3;
        // 판넬 간격(6) 안의 안내 폭(5.5) 안에 플레이어가 들어오면 지나가는 것으로 봅니다.
        private const float _panelHalfWidth = 2.75f;
        private const float _videoWidth = 4f, _videoHeight = 2.25f, _videoFrame = .1f;
        // 문구 윗변과 영상 아랫변 사이의 여백입니다.
        private const float _videoGap = .1f;
        private UI_Lang[] _entries;
        private TextMesh[] _labels;
        private GameObject[] _screens;
        private RawImage[] _images;
        private bool[] _played;
        private VideoPlayer _video;
        private SpatialRuntime _player;
        private LanguageTextSource _text;
        private SubscriptionScope _scope;
        private int _playing = -1;
        private int _loops;

        public static void AddTo(Transform map, Transform markers, TextMesh prototype, IReadOnlyDataCatalog data, SpatialRuntime player)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            UI_Lang[] entries = data.GetTable<UI_Lang>().All.Where(row => row.Key.StartsWith(_keyPrefix, StringComparison.Ordinal)).OrderBy(row => row.ID).ToArray();
            if (entries.Length == 0) throw new InvalidOperationException("UI_Lang에 StartGuide 시작 안내 연결이 없습니다.");
            var text = new LanguageTextSource(data);
            // 연결 검사는 생성 전에 끝내 중간 생성 객체가 남지 않게 합니다. 영상은 두 키 배치 모두 확인합니다.
            foreach (UI_Lang entry in entries)
            {
                string action = ReadAction(entry);
                string.Format(text.Read(entry.TextID, LanguageCode.Korean), KeyboardControls.ReadActionKey(action, id => text.ReadUi(id, LanguageCode.Korean)));
                string.Format(text.Read(entry.TextID, LanguageCode.English), KeyboardControls.ReadActionKey(action, id => text.ReadUi(id, LanguageCode.English)));
                ResolveVideo(action, KeyboardLayout.Arrows);
                ResolveVideo(action, KeyboardLayout.Wasd);
            }
            if (map == null || markers == null || prototype == null || player == null)
                throw new System.ArgumentException("시작 안내의 맵·표식·글꼴·플레이어를 연결해 주세요.");
            Transform start = markers.Find("PlayerStart");
            if (start == null) throw new System.InvalidOperationException("시작 안내의 PlayerStart가 없습니다.");
            var root = new GameObject("StartControls"); root.SetActive(false); root.transform.SetParent(map, false);
            try
            {
                var view = root.AddComponent<StartMapControlsView>(); view._text = text; view._entries = entries; view._player = player;
                view._labels = new TextMesh[entries.Length];
                view._screens = new GameObject[entries.Length];
                view._images = new RawImage[entries.Length];
                view._played = new bool[entries.Length];
                view._video = CreateVideoPlayer(root);
                for (int i = 0; i < view._labels.Length; i++)
                {
                    var label = new GameObject("Action_" + i, typeof(TextMesh)); label.transform.SetParent(root.transform, false);
                    label.transform.position = start.position + new Vector3(3f + i * 6f, 2.2f, 0f);
                    var mesh = label.GetComponent<TextMesh>(); mesh.font = prototype.font; mesh.fontSize = 96;
                    mesh.characterSize = .031f; mesh.anchor = TextAnchor.MiddleCenter; mesh.alignment = TextAlignment.Center;
                    mesh.fontStyle = FontStyle.Bold; mesh.color = new Color(1f,.96f,.86f,1f);
                    var renderer = label.GetComponent<MeshRenderer>(); renderer.sharedMaterial = prototype.GetComponent<MeshRenderer>().sharedMaterial;
                    renderer.sortingOrder = 62; view._labels[i] = mesh;
                    view._screens[i] = CreateScreen(label.transform, renderer.sortingLayerID, out view._images[i]);
                }
                root.SetActive(true);
            }
            catch
            {
                Destroy(root);
                throw;
            }
        }

        private static string ReadAction(UI_Lang entry) => entry.Key.Substring(_keyPrefix.Length);

        // A(방향키)·B(WASD) 영상이 따로 있으면 그것을, 두 배치의 키가 같은 동작은 접미사 없는 영상을 씁니다.
        private static string ResolveVideo(string action, KeyboardLayout layout)
        {
            string suffix = layout == KeyboardLayout.Wasd ? "_B" : "_A";
            foreach (string name in new[] { "StartGuide_" + action + suffix, "StartGuide_" + action })
            {
                string address = StreamingVideoAddress.Prefix + "Video/" + name + ".mp4";
                if (StreamingVideoAddress.TryResolve(address, Application.streamingAssetsPath, out string path) && File.Exists(path)) return path;
            }
            throw new InvalidOperationException($"시작 안내 영상이 없습니다: StreamingAssets/Video/StartGuide_{action}{suffix}.mp4 또는 StartGuide_{action}.mp4");
        }

        // 타이틀 영상과 같이 StreamingAssets 원본을 직접 읽습니다. 하나의 재생기를 판넬이 차례로 사용합니다.
        private static VideoPlayer CreateVideoPlayer(GameObject root)
        {
            var video = root.AddComponent<VideoPlayer>();
            video.playOnAwake = false; video.source = VideoSource.Url;
            video.renderMode = VideoRenderMode.APIOnly;
            video.audioOutputMode = VideoAudioOutputMode.None;
            video.timeUpdateMode = VideoTimeUpdateMode.GameTime;
            // 반복 지점마다 loopPointReached로 재생 횟수를 셉니다.
            video.isLooping = true; video.skipOnDrop = true;
            return video;
        }

        // 문구 바로 위의 월드 화면입니다. 재생 중에만 켜며 높이는 문구 갱신 때 맞춥니다.
        private static GameObject CreateScreen(Transform label, int sortingLayerId, out RawImage image)
        {
            var screen = new GameObject("GuideVideo", typeof(RectTransform), typeof(Canvas), typeof(Image));
            screen.SetActive(false);
            screen.transform.SetParent(label, false);
            var canvas = screen.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingLayerID = sortingLayerId; canvas.sortingOrder = 63;
            var rect = (RectTransform)screen.transform;
            rect.sizeDelta = new Vector2(_videoWidth + _videoFrame * 2f, _videoHeight + _videoFrame * 2f);
            var frame = screen.GetComponent<Image>();
            frame.color = new Color(.025f, .02f, .015f, .85f); frame.raycastTarget = false;
            var picture = new GameObject("Picture", typeof(RectTransform), typeof(RawImage));
            picture.transform.SetParent(screen.transform, false);
            var pictureRect = (RectTransform)picture.transform;
            pictureRect.anchorMin = Vector2.zero; pictureRect.anchorMax = Vector2.one;
            pictureRect.offsetMin = new Vector2(_videoFrame, _videoFrame); pictureRect.offsetMax = new Vector2(-_videoFrame, -_videoFrame);
            image = picture.GetComponent<RawImage>();
            image.raycastTarget = false;
            return screen;
        }

        private void OnEnable()
        {
            if (_labels == null) return;
            var scope = _scope = new SubscriptionScope();
            System.Action refresh = scope.Guard(RefreshSafely);
            System.Action controls = scope.Guard(OnControlsChanged);
            System.Action moved = scope.Guard(CheckPlayer);
            scope.Add(() => KeyboardControls.Changed -= controls); KeyboardControls.Changed += controls;
            scope.Add(() => LanguageSettings.Changed -= refresh); LanguageSettings.Changed += refresh;
            scope.Add(() => _player.SpatialChanged -= moved); _player.SpatialChanged += moved;
            scope.Add(() => _video.prepareCompleted -= OnPrepared); _video.prepareCompleted += OnPrepared;
            scope.Add(() => _video.loopPointReached -= OnLoopPoint); _video.loopPointReached += OnLoopPoint;
            scope.Add(() => _video.errorReceived -= OnVideoError); _video.errorReceived += OnVideoError;
            RefreshSafely();
            CheckPlayer();
        }
        private void OnDisable()
        {
            // 끝까지 보지 못한 판넬은 다시 활성화된 뒤 지나갈 때 재생할 수 있게 둡니다.
            if (_playing >= 0) _played[_playing] = false;
            SubscriptionScope.Cleanup(() => { _scope?.Dispose(); _scope = null; }, StopVideo);
        }
        private void RefreshSafely()
        {
            try { Refresh(); }
            catch (Exception error) { ErrorPopupHost.WriteError(error, this); }
        }
        // 키 배치가 바뀌면 문구를 갱신하고 재생 중인 판넬은 새 배치 영상으로 처음부터 다시 재생합니다.
        private void OnControlsChanged()
        {
            RefreshSafely();
            if (_playing >= 0) Play(_playing);
        }
        private void Refresh()
        {
            for (int i = 0; i < _labels.Length; i++)
            {
                UI_Lang entry = _entries[i];
                string key = KeyboardControls.ReadActionKey(ReadAction(entry), _text.ReadUi);
                TextMesh label = _labels[i];
                label.characterSize = .031f;
                label.text = Wrap(string.Format(_text.Read(entry.TextID), key));
                Vector3 size = label.GetComponent<MeshRenderer>().bounds.size;
                float scale = Mathf.Min(1f, 5.1f / Mathf.Max(size.x, .001f), 2.2f / Mathf.Max(size.y, .001f));
                // 안내 영역(5.1×2.2)은 유지하고 글자 크기만 표시 경계에서 보정합니다.
                label.characterSize = .031f * scale;
                // 글자 크기·줄 수가 바뀌므로 영상 아랫변을 실제 문구 윗변 바로 위에 다시 맞춥니다.
                float textTop = label.GetComponent<MeshRenderer>().bounds.max.y - label.transform.position.y;
                var screen = (RectTransform)_screens[i].transform;
                screen.localPosition = new Vector3(0f, textTop + _videoGap + screen.sizeDelta.y * .5f, 0f);
            }
        }

        // 위치 변경 통지 때만 판정합니다. 비교는 플레이어의 정수 좌표 단위로 합니다.
        private void CheckPlayer()
        {
            if (_player == null || !_player.isActiveAndEnabled) return;
            long x = _player.Position.X;
            long half = _player.ToInteger(_panelHalfWidth);
            for (int i = 0; i < _labels.Length; i++)
            {
                if (_played[i] || i == _playing) continue;
                long center = _player.ToInteger(_labels[i].transform.position.x);
                if (Math.Abs(x - center) > half) continue;
                // 판넬당 한 번만 재생합니다. 다른 판넬로 넘어가면 이전 영상은 멈춥니다.
                _played[i] = true;
                Play(i);
                return;
            }
        }

        private void Play(int index)
        {
            try
            {
                StopVideo();
                _playing = index; _loops = 0;
                _video.url = new Uri(ResolveVideo(ReadAction(_entries[index]), KeyboardControls.Layout)).AbsoluteUri;
                _video.Prepare();
            }
            catch (Exception error)
            {
                StopVideo();
                ErrorPopupHost.WriteError(error, this);
            }
        }

        private void OnPrepared(VideoPlayer source)
        {
            if (source != _video || _playing < 0 || !isActiveAndEnabled) return;
            _images[_playing].texture = source.texture;
            _screens[_playing].SetActive(true);
            source.Play();
        }

        private void OnLoopPoint(VideoPlayer source)
        {
            if (source != _video || _playing < 0) return;
            _loops++;
            // 정해진 횟수를 다 보면 멈추고 영상 화면만 숨깁니다. 키 문구 판넬은 남습니다.
            if (_loops >= _playCount) StopVideo();
        }

        // 표시 오류는 이 판넬이 정리·기록하고 게임 진행은 막지 않습니다.
        private void OnVideoError(VideoPlayer source, string message)
        {
            if (source != _video) return;
            StopVideo();
            ErrorPopupHost.WriteError(new InvalidOperationException("시작 안내 영상 재생 실패: " + message), this);
        }

        private void StopVideo()
        {
            if (_video != null) _video.Stop();
            if (_playing >= 0)
            {
                if (_images[_playing] != null) _images[_playing].texture = null;
                if (_screens[_playing] != null) _screens[_playing].SetActive(false);
            }
            _playing = -1; _loops = 0;
        }

        private static string Wrap(string value)
        {
            var result = new StringBuilder();
            int width = 0;
            foreach (string word in value.Split(' '))
            {
                int wordWidth = word.Sum(character => character > 127 ? 2 : 1);
                if (width > 0)
                {
                    bool nextLine = width + wordWidth + 1 > 32;
                    result.Append(nextLine ? '\n' : ' ');
                    width = nextLine ? 0 : width + 1;
                }
                result.Append(word); width += wordWidth;
            }
            return result.ToString();
        }
    }
}
