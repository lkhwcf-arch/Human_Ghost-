using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Storage;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HumanGhost.Gameplay.Presentation
{
    // 씬 이동 동안의 로딩 화면(O07·O07_1·O07_2)입니다. 그림은 Inspector에서 교체합니다. 지금 그림은 임시입니다.
    public class LoadingScreen : MonoBehaviour
    {
        private const int _minimumMs = 2000;
        private const int _stampMs = 150;
        private const int _appearMs = 300;
        private const int _oneLineHeight = 96;
        private const int _twoLineHeight = 141;
        private static int _previousTipId;
        private static int _previousBackground = -1;

        [SerializeField] private CanvasGroup _group = null;
        [SerializeField] private Image _background = null;
        [SerializeField, InspectorName("랜덤 배경 그림")] private Sprite[] _backgrounds = Array.Empty<Sprite>();
        [SerializeField] private RectTransform _tipBox = null;
        [SerializeField] private Text _tip = null;
        [SerializeField] private RectTransform _seal = null;
        private AsyncOperation _load;
        private Task<bool> _ready;
        private bool _begun;
        private bool _fadeOnly;
        private string _sceneName;
        private int _phase;
        private int _elapsed;
        private MillisecondClock _clock;
        private static LoadingScreen _active;
        public static bool IsActive => _active != null;
        private Vector2 _boxPosition;

        public void Begin(string sceneName, IReadOnlyDataCatalog data, Task<bool> ready = null)
        { _ready = ready; BeginInternal(sceneName, data, false); }

        // 타이틀 복귀는 같은 이동 경로를 사용하되 로딩 문구·최소 대기 없이 암전만 표시합니다.
        public void BeginFade(string sceneName) => BeginInternal(sceneName, null, true);

        private void BeginInternal(string sceneName, IReadOnlyDataCatalog data, bool fadeOnly)
        {
            if (_begun) throw new InvalidOperationException("이미 시작된 로딩 화면입니다.");
            try
            {
                if (IsActive) throw new InvalidOperationException("다른 씬 이동이 이미 진행 중입니다.");
                if (!isActiveAndEnabled) throw new InvalidOperationException("씬 이동 화면은 활성 상태에서 시작해 주세요.");
                if (string.IsNullOrEmpty(sceneName)) throw new ArgumentException("이동할 씬이 필요합니다.", nameof(sceneName));
                if (!Application.CanStreamedLevelBeLoaded(sceneName))
                    throw new InvalidOperationException("이동할 씬이 빌드 목록에 없거나 로딩할 수 없습니다: " + sceneName);
                if (_group == null || _background == null || _tipBox == null || _tip == null || _seal == null)
                    throw new InvalidOperationException("로딩 화면의 표시 참조를 모두 연결해 주세요.");
                ScreenVisibility.RequireVisible(_group.GetComponentInParent<Canvas>());
                _fadeOnly = fadeOnly;
                _sceneName = sceneName;
                _phase = 0;
                _elapsed = 0;
                _group.alpha = 0f;
                _group.blocksRaycasts = true;
                _boxPosition = _tipBox.anchoredPosition;
                if (fadeOnly)
                {
                    _background.enabled = true;
                    _background.sprite = null;
                    _background.color = Color.black;
                    _tipBox.gameObject.SetActive(false);
                    _seal.gameObject.SetActive(false);
                }
                else { ShowBackground(); ShowTip(data); StartLoad(); }
                _begun = true;
                _active = this;
                DontDestroyOnLoad(gameObject);

            }
            catch { Destroy(gameObject); throw; }
        }

        private void ShowBackground()
        {
            // 그림이 하나도 없으면 검은 바탕을 씁니다(O07_1).
            _background.enabled = _backgrounds.Length > 0;
            if (_backgrounds.Length == 0) return;
            int index = UnityEngine.Random.Range(0, _backgrounds.Length);
            if (index == _previousBackground && _backgrounds.Length > 1) index = (index + 1) % _backgrounds.Length;
            _previousBackground = index;
            _background.sprite = _backgrounds[index];
        }

        private void ShowTip(IReadOnlyDataCatalog data)
        {
            var ids = new List<int>();
            IReadOnlyDataTable<Language> language = data.GetTable<Language>();
            foreach (Language row in language.All) ids.Add(row.ID);
            int id = LoadingTip.Pick(ids, _previousTipId, UnityEngine.Random.Range);
            _previousTipId = id;
            if (id == 0) { _tipBox.gameObject.SetActive(false); return; }
            LoadingTip.Split(language.Get(id).KO, out string title, out string body);
            _tip.text = title.Length == 0 ? "<color=#C8C2B6>" + body + "</color>"
                : "<color=#ECE6DA><b>" + title + "</b></color><color=#C8C2B6>" + body + "</color>";
            Canvas.ForceUpdateCanvases();
            int lines = _tip.cachedTextGenerator.lineCount;
            _tipBox.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, lines >= 2 ? _twoLineHeight : _oneLineHeight);
        }

        private void StartLoad()
        {
            _load = SceneManager.LoadSceneAsync(_sceneName);
            if (_load == null) throw new InvalidOperationException("씬 로딩 작업을 만들지 못했습니다: " + _sceneName);
            _load.allowSceneActivation = false;
        }

        // 실제 씬 읽기는 Unity 비동기 작업에 맡기고 화면 진행은 한 실행 흐름에서 처리합니다.
        protected virtual void Update()
        {
            if (!_begun) return;
            try
            {
                _elapsed += Math.Min(_clock.Advance(Time.unscaledDeltaTime), int.MaxValue - _elapsed);
                if (!_fadeOnly) _seal.Rotate(0f, 0f, -240f * Time.unscaledDeltaTime);
                if (_phase == 0)
                {
                    float appear = Mathf.Clamp01(_elapsed / (float)_appearMs);
                    _group.alpha = appear;
                    if (!_fadeOnly) _tipBox.anchoredPosition = _boxPosition + Vector2.down * (8f * (1f - appear));
                    if (appear < 1f) return;
                    if (_fadeOnly) StartLoad();
                    _phase = 1;
                }
                if (_phase == 1)
                {
                    float progress = _fadeOnly ? Mathf.Clamp01(_load.progress / 0.9f)
                        : LoadingTip.Progress(_load.progress / 0.9f, _elapsed / 1000f, _minimumMs / 1000f);

                    if (progress < 1f) return;
                    _elapsed = 0;
                    _phase = 2;
                }
                if (_phase == 2)
                {
                    if (!_fadeOnly)
                    {
                        _seal.localScale = Vector3.one * Mathf.Lerp(1.1f, 1f, Mathf.Clamp01(_elapsed / (float)_stampMs));
                        if (_elapsed < _stampMs) return;
                    }
                    _load.allowSceneActivation = true;
                    _phase = 3;
                }
                if (_phase == 3)
                {
                    if (!_load.isDone) return;
                    if (_ready != null)
                    {
                        if (!_ready.IsCompleted) return;
                        if (_ready.IsCanceled || _ready.IsFaulted || !_ready.Result)
                            throw new InvalidOperationException("이동한 공간의 게임 상태 준비·복원이 완료되지 않았습니다.");
                    }
                    _elapsed = 0;
                    _phase = 4;
                }
                if (_phase == 4)
                {
                    _group.alpha = 1f - Mathf.Clamp01(_elapsed / (float)_appearMs);
                    if (_elapsed >= _appearMs) { _begun = false; Destroy(gameObject); }
                }
            }
            catch (Exception error)
            {
                Debug.LogException(new InvalidOperationException("씬 이동 화면 처리에 실패했습니다.", error), this);
                Destroy(gameObject);
            }
        }

        private void OnDisable()
        {
            if (_active == this) _active = null;
            // Unity 씬 로드는 취소할 수 없습니다. 화면 종료가 활성화 대기를 영구 유지하게 하지 않습니다.
            if (_load != null && !_load.isDone) _load.allowSceneActivation = true;
            if (_begun) Destroy(gameObject);
        }
    }
}
