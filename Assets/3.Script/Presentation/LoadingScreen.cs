using System;
using System.Collections;
using System.Collections.Generic;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Storage;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HumanGhost.Gameplay.Presentation
{
    // 씬 이동 동안의 로딩 화면(O07·O07_1·O07_2)입니다. 그림은 Inspector에서 교체합니다. 지금 그림은 임시입니다.
    public sealed class LoadingScreen : MonoBehaviour
    {
        private const float _minimumSeconds = 2f;
        private const float _stampSeconds = 0.15f;
        private const float _appearSeconds = 0.3f;
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
        [SerializeField] private Image _sealFill = null;
        private AsyncOperation _load;
        private bool _begun;

        public void Begin(string sceneName, IReadOnlyDataCatalog data)
        {
            if (_begun) throw new InvalidOperationException("이미 시작된 로딩 화면입니다.");
            try
            {
                if (string.IsNullOrEmpty(sceneName)) throw new ArgumentException("이동할 씬이 필요합니다.", nameof(sceneName));
                if (!Application.CanStreamedLevelBeLoaded(sceneName))
                    throw new InvalidOperationException("이동할 씬이 빌드 목록에 없거나 로딩할 수 없습니다: " + sceneName);
                if (_group == null || _background == null || _tipBox == null || _tip == null || _seal == null || _sealFill == null)
                    throw new InvalidOperationException("로딩 화면의 표시 참조를 모두 연결해 주세요.");
                ShowBackground();
                ShowTip(data);
                _load = SceneManager.LoadSceneAsync(sceneName);
                if (_load == null) throw new InvalidOperationException("씬 로딩 작업을 만들지 못했습니다: " + sceneName);
                _load.allowSceneActivation = false;
                _begun = true;
                DontDestroyOnLoad(gameObject);
                StartCoroutine(Play());
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

        private IEnumerator Play()
        {
            AsyncOperation load = _load;
            float elapsed = 0f;
            float progress = 0f;
            Vector2 boxPosition = _tipBox.anchoredPosition;
            while (progress < 1f)
            {
                elapsed += Time.unscaledDeltaTime;
                // AsyncOperation은 활성화 대기 중 0.9에서 멈춥니다.
                progress = LoadingTip.Progress(load.progress / 0.9f, elapsed, _minimumSeconds);
                _sealFill.fillAmount = progress;
                float appear = Mathf.Clamp01(elapsed / _appearSeconds);
                _group.alpha = appear;
                _tipBox.anchoredPosition = boxPosition + Vector2.down * (8f * (1f - appear));
                yield return null;
            }
            // 다 차면 도장을 찍듯 한 번 눌린 뒤 장면이 바뀝니다.
            for (float stamp = 0f; stamp < _stampSeconds; stamp += Time.unscaledDeltaTime)
            {
                _seal.localScale = Vector3.one * Mathf.Lerp(1.1f, 1f, stamp / _stampSeconds);
                yield return null;
            }
            _seal.localScale = Vector3.one;
            load.allowSceneActivation = true;
            while (!load.isDone) yield return null;
            for (float fade = 0f; fade < _appearSeconds; fade += Time.unscaledDeltaTime)
            {
                _group.alpha = 1f - fade / _appearSeconds;
                yield return null;
            }
            Destroy(gameObject);
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            // Unity 씬 로드는 취소할 수 없습니다. 화면 종료가 활성화 대기를 영구 유지하게 하지 않습니다.
            if (_load != null && !_load.isDone) _load.allowSceneActivation = true;
            if (_begun) Destroy(gameObject);
        }
    }
}
