using System;
using System.Collections.Generic;
using HumanGhost.Gameplay.Adapters;
using UnityEngine;
using UnityEngine.UI;

namespace HumanGhost.Gameplay.Presentation
{
    // 텍스트박스·목표 안내 띠·해금 알림(O10)입니다. 진행 규칙은 TextboxPlayback이 맡습니다. 프레임 그림은 공급 뒤 교체합니다.
    public sealed class TextboxScreen : MonoBehaviour, ISkipTarget
    {
        private const int _millisecondsPerCharacter = 30;
        private const float _objectiveSeconds = 4f;
        private const float _noticeSeconds = 2.5f;
        [SerializeField] private GameObject _box = null;
        [SerializeField] private Text _speaker = null;
        [SerializeField] private Text _body = null;
        [SerializeField] private GameObject _nextHint = null;
        [SerializeField] private GameObject _objective = null;
        [SerializeField] private Text _objectiveText = null;
        [SerializeField] private GameObject _notice = null;
        [SerializeField] private Text _noticeText = null;
        [SerializeField] private StageNameBanner _stageName = null;
        [SerializeField, InspectorName("스킵 안내")] private SkipHint _skipHint = null;
        private TextboxPlayback _playback;
        private Action _closed;
        private float _objectiveRemaining, _noticeRemaining;
        private MillisecondClock _clock;
        private string _shownLine;
        private int _shownCharacters = -1;
        private bool _unscaledPlayback;
        private int _shownSpeakerId = -1;
        private bool _playbackBlocked;
        // 시연 마무리 안내(A2): 화면 전체를 검게 덮고 가운데 문구를 띄웁니다. 시간이 지나거나 넘기기 입력으로 닫힙니다.
        private GameObject _blackout;
        private Text _blackoutText;
        private float _blackoutRemaining;
        private Action _blackoutClosed;
        public bool IsBlackoutOpen => _blackoutClosed != null;

        public bool IsOpen => _playback != null || IsBlackoutOpen;
        public bool LastPlaybackCompleted { get; private set; }
        public int CurrentTextId => _playback?.CurrentTextId ?? 0;
        public event Action<bool> OpenChanged;
        public event Action SkipAvailabilityChanged;
        public void ConnectSkipHint(LanguageTextSource text, LanguageCode language)
        {
            if (_skipHint == null) throw new InvalidOperationException("대사 화면의 공용 스킵 안내를 연결해 주세요.");
            _skipHint.Connect(this, SkipScope.Dialogue, text, language);
        }
        public void BlockSkipHint(bool blocked) { _playbackBlocked = blocked; if (_skipHint != null) _skipHint.SetBlocked(blocked); }
        public void DisconnectSkipHint() { if (_skipHint != null) _skipHint.Disconnect(); }
        private void OnEnable() => SkipAvailabilityChanged?.Invoke();
        private void OnDisable() => SkipAvailabilityChanged?.Invoke();

        private void Awake()
        {
            _box.SetActive(false); _objective.SetActive(false); _notice.SetActive(false);
        }

        public void Open(string speaker, IReadOnlyList<string> lines, Action closed)
        {
            if (IsOpen) return;
            OpenPlayback(speaker, new TextboxPlayback(lines, _millisecondsPerCharacter), closed);
        }

        // 새 대사는 Language ID 한 개를 한 항목으로 공급합니다. 기존 임시 문구로 대체하지 않습니다.
        public void Open(int speakerTextId, IReadOnlyList<int> textIds, LanguageTextSource text, LanguageCode language, Action closed)
        {
            if (IsOpen) return;
            if (text == null) throw new ArgumentNullException(nameof(text));
            string speaker = speakerTextId == 0 ? "" : text.Read(speakerTextId, language);
            if (_skipHint != null) _skipHint.Connect(this, SkipScope.Dialogue, text, language);
            OpenPlayback(speaker, new TextboxPlayback(textIds, id => text.Read(id, language), _millisecondsPerCharacter), closed);
        }

        public void Open(IReadOnlyList<DialogueLine> lines, LanguageTextSource text, LanguageCode language, bool useUnscaledTime, Action closed)
        {
            if (IsOpen) return;
            _textSource = text ?? throw new ArgumentNullException(nameof(text));
            _textLanguage = language;
            if (_skipHint != null) _skipHint.Connect(this, SkipScope.Dialogue, text, language);
            OpenPlayback("", new TextboxPlayback(lines, id => text.Read(id, language), _millisecondsPerCharacter), closed);
            _unscaledPlayback = useUnscaledTime;
        }
        private LanguageTextSource _textSource;
        private LanguageCode _textLanguage;
        private void OpenPlayback(string speaker, TextboxPlayback playback, Action closed)
        {
            _unscaledPlayback = false; _shownSpeakerId = -1;
            LastPlaybackCompleted = false;
            _playback = playback;
            _closed = closed; _clock = default;
            _shownLine = null; _shownCharacters = -1;
            _speaker.text = speaker;
            _box.SetActive(true);
            Refresh();
            OpenChanged?.Invoke(true);
            SkipAvailabilityChanged?.Invoke();
        }

        public void Advance()
        {
            if (IsBlackoutOpen) { CloseBlackout(); return; }
            if (_playback == null) return;
            _playback.Advance();
            if (_playback.IsFinished) Close(); else Refresh();
        }

        public bool CanSkip(SkipScope scope) => scope == SkipScope.Dialogue && isActiveAndEnabled && _playback != null;
        public bool TrySkip(SkipScope scope)
        {
            if (!CanSkip(scope) || !_playback.SkipLine()) return false;
            if (_playback.IsFinished) Close(); else Refresh();
            return true;
        }

        public void Close()
        {
            if (_playback == null) return;
            LastPlaybackCompleted = _playback.IsFinished;
            _playback = null;
            _textSource = null;
            SkipAvailabilityChanged?.Invoke();
            _box.SetActive(false);
            OpenChanged?.Invoke(false);
            Action closed = _closed;
            _closed = null;
            closed?.Invoke();
        }

        public void ShowStageName(string text) => _stageName.Show(text);

        public bool OpenBlackout(string text, int milliseconds, Action closed)
        {
            if (IsOpen) return false;
            if (milliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(milliseconds), "안내 표시 시간은 양수 밀리초여야 합니다.");
            if (_body == null) throw new InvalidOperationException("대사 글자 표시를 연결해 주세요.");
            if (_blackout == null) CreateBlackout();
            _blackoutText.text = text;
            _blackoutRemaining = milliseconds / 1000f;
            _blackoutClosed = closed ?? (() => { });
            _blackout.transform.SetAsLastSibling();
            _blackout.SetActive(true);
            OpenChanged?.Invoke(true);
            return true;
        }

        // 그림 리소스 없이 대사 글꼴을 재사용해 만듭니다. 대사창과 같은 최상위 캔버스의 맨 앞에 둡니다.
        private void CreateBlackout()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) throw new InvalidOperationException("대사창이 캔버스 아래에 있어야 합니다.");
            _blackout = new GameObject("EndingBlackout", typeof(RectTransform), typeof(Image));
            _blackout.SetActive(false);
            var area = (RectTransform)_blackout.transform;
            area.SetParent(canvas.rootCanvas.transform, false);
            area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one; area.offsetMin = Vector2.zero; area.offsetMax = Vector2.zero;
            _blackout.GetComponent<Image>().color = Color.black;
            var label = new GameObject("Text", typeof(RectTransform), typeof(Text));
            var labelArea = (RectTransform)label.transform;
            labelArea.SetParent(area, false);
            labelArea.anchorMin = new Vector2(0.1f, 0.3f); labelArea.anchorMax = new Vector2(0.9f, 0.7f);
            labelArea.offsetMin = Vector2.zero; labelArea.offsetMax = Vector2.zero;
            _blackoutText = label.GetComponent<Text>();
            _blackoutText.font = _body.font;
            _blackoutText.fontSize = _body.fontSize;
            _blackoutText.color = Color.white;
            _blackoutText.alignment = TextAnchor.MiddleCenter;
            _blackoutText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _blackoutText.verticalOverflow = VerticalWrapMode.Overflow;
            _blackoutText.raycastTarget = false;
        }

        private void CloseBlackout()
        {
            if (_blackoutClosed == null) return;
            Action closed = _blackoutClosed;
            _blackoutClosed = null;
            if (_blackout != null) _blackout.SetActive(false);
            OpenChanged?.Invoke(false);
            closed();
        }

        // 최상위 캔버스에 붙였으므로 이 화면이 사라질 때 함께 정리합니다.
        private void OnDestroy()
        {
            _blackoutClosed = null;
            if (_blackout != null) Destroy(_blackout);
        }

        public void ShowObjective(string text)
        {
            _objectiveText.text = text;
            _objective.SetActive(true);
            _objectiveRemaining = _objectiveSeconds;
        }

        public void ShowNotice(string text)
        {
            _noticeText.text = text;
            _notice.SetActive(true);
            _noticeRemaining = _noticeSeconds;
        }

        private void Update()
        {
            if (IsBlackoutOpen && (_blackoutRemaining -= Time.unscaledDeltaTime) <= 0f) CloseBlackout();
            if (_objective.activeSelf && (_objectiveRemaining -= Time.unscaledDeltaTime) <= 0f) _objective.SetActive(false);
            if (_notice.activeSelf && (_noticeRemaining -= Time.unscaledDeltaTime) <= 0f) _notice.SetActive(false);
            if (_playback == null) return;
            if (_unscaledPlayback && _playbackBlocked) return;
            // 글자 출력은 일시정지 중 멈춥니다(O01_2 #6). 히트스톱과 같은 시간을 씁니다.
            _playback.Tick(_clock.Advance(_unscaledPlayback ? Time.unscaledDeltaTime : Time.deltaTime));
            Refresh();
        }

        private void Refresh()
        {
            int speaker = _playback.CurrentSpeakerTextId;
            if (speaker != 0 && speaker != _shownSpeakerId)
            { _speaker.text = _textSource.Read(speaker, _textLanguage); _shownSpeakerId = speaker; }
            string line = _playback.CurrentLine;
            if (line == _shownLine && _shownCharacters == _playback.VisibleCharacters) return;
            _shownLine = line; _shownCharacters = _playback.VisibleCharacters;
            _body.text = line.Substring(0, _playback.VisibleCharacters);
            _nextHint.SetActive(_playback.IsLineComplete);
        }
    }
}
