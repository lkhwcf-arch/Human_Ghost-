using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HumanGhost.Gameplay.Presentation
{
    // 텍스트박스·목표 안내 띠·해금 알림(O10)입니다. 진행 규칙은 TextboxPlayback이 맡습니다. 프레임 그림은 공급 뒤 교체합니다.
    public sealed class TextboxScreen : MonoBehaviour
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
        private TextboxPlayback _playback;
        private Action _closed;
        private float _objectiveRemaining, _noticeRemaining;
        private MillisecondClock _clock;
        private string _shownLine;
        private int _shownCharacters = -1;

        public bool IsOpen => _playback != null;
        public event Action<bool> OpenChanged;

        private void Awake()
        {
            _box.SetActive(false); _objective.SetActive(false); _notice.SetActive(false);
        }

        public void Open(string speaker, IReadOnlyList<string> lines, Action closed)
        {
            if (IsOpen) return;
            _playback = new TextboxPlayback(lines, _millisecondsPerCharacter);
            _closed = closed; _clock = default;
            _shownLine = null; _shownCharacters = -1;
            _speaker.text = speaker;
            _box.SetActive(true);
            Refresh();
            OpenChanged?.Invoke(true);
        }

        public void Advance()
        {
            if (_playback == null) return;
            _playback.Advance();
            if (_playback.IsFinished) Close(); else Refresh();
        }

        public void Close()
        {
            if (_playback == null) return;
            _playback = null;
            _box.SetActive(false);
            OpenChanged?.Invoke(false);
            Action closed = _closed;
            _closed = null;
            closed?.Invoke();
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
            if (_objective.activeSelf && (_objectiveRemaining -= Time.unscaledDeltaTime) <= 0f) _objective.SetActive(false);
            if (_notice.activeSelf && (_noticeRemaining -= Time.unscaledDeltaTime) <= 0f) _notice.SetActive(false);
            if (_playback == null) return;
            // 글자 출력은 일시정지 중 멈춥니다(O01_2 #6). 히트스톱과 같은 시간을 씁니다.
            _playback.Tick(_clock.Advance(Time.deltaTime));
            Refresh();
        }

        private void Refresh()
        {
            string line = _playback.CurrentLine;
            if (line == _shownLine && _shownCharacters == _playback.VisibleCharacters) return;
            _shownLine = line; _shownCharacters = _playback.VisibleCharacters;
            _body.text = line.Substring(0, _playback.VisibleCharacters);
            _nextHint.SetActive(_playback.IsLineComplete);
        }
    }
}
