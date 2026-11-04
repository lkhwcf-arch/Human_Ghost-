using System;
using System.IO;
using System.Collections;
using HumanGhost.Data.Infrastructure;
using HumanGhost.Gameplay.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UI;

namespace HumanGhost.Gameplay.Adapters
{
    // 타이틀 저장 유무·새 게임 진입·임시 옵션 화면을 연결합니다. 인트로·이어하기 복원은 소유하지 않습니다.
    public sealed class TitleGameEntry : MonoBehaviour
    {
        [SerializeField] private TitleSceneHost _host = null;
        [SerializeField] private TitleSceneActions _actions = null;
        [SerializeField] private GameDataInitializer _data = null;
        [SerializeField, InspectorName("로딩 화면 프리팹")] private LoadingScreen _loadingPrefab = null;
        [SerializeField, InspectorName("소리 프리팹")] private GameAudio _audioPrefab = null;
        private GameAudio _audio;
        [SerializeField, InspectorName("타이틀 배경음 키")] private string _musicKey = "BGM_VILLAGE";
        private GameObject _settingsCanvas;
        private IDisposable _settingsDismiss;
        private int _settingsOpenedFrame;
        private bool _settingsPending;
        private Coroutine _settingsReturn;
        [SerializeField, InspectorName("새 게임 시작 씬")] private string _newGameScene = "MapDohwagolTest";
        [SerializeField, InspectorName("저장 하위 폴더")] private string _saveSubdirectory = "Saves/Demo";
        [SerializeField, InspectorName("지원 저장 버전")] private int _saveVersion = 1;

        private void OnEnable()
        {
            _actions.Connect(TitleAction.NewGame, StartNewGame);
            _actions.Connect(TitleAction.Settings, OpenSettings);
        }

        private void OnDisable()
        {
            _actions.Disconnect(TitleAction.NewGame, StartNewGame);
            _actions.Disconnect(TitleAction.Settings, OpenSettings);
            _settingsDismiss?.Dispose();
            _settingsDismiss = null;
            if (_settingsReturn != null) StopCoroutine(_settingsReturn);
            _settingsReturn = null;
            if (_settingsCanvas != null) _settingsCanvas.SetActive(false);
            if (_settingsPending && _host != null)
            {
                _host.CompleteRequest();
                _host.enabled = true;
            }
            _settingsPending = false;
        }

        // 정식 설정 화면 연결 전의 임시 화면입니다. 진입 입력으로 바로 닫히지 않게 같은 프레임은 무시합니다.
        private bool OpenSettings()
        {
            if (_settingsPending) return false;
            if (_settingsCanvas == null)
            {
                _settingsCanvas = new GameObject("TitleSettingsPlaceholder", typeof(RectTransform), typeof(Canvas), typeof(Image));
                _settingsCanvas.SetActive(false);
                _settingsCanvas.transform.SetParent(transform, false);
                Canvas canvas = _settingsCanvas.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 1000;
                _settingsCanvas.GetComponent<Image>().color = Color.black;
            }
            _settingsOpenedFrame = Time.frameCount;
            _settingsPending = true;
            _host.enabled = false;
            _settingsCanvas.SetActive(true);
            Cursor.visible = true;
            _settingsDismiss = InputSystem.onAnyButtonPress.Call(_ => DismissSettings());
            return true;
        }

        private void DismissSettings()
        {
            if (!_settingsPending || Time.frameCount <= _settingsOpenedFrame || _settingsReturn != null) return;
            _settingsDismiss?.Dispose();
            _settingsDismiss = null;
            _settingsCanvas.SetActive(false);
            // 닫는 입력이 같은 프레임에 타이틀 결정으로 다시 전달되지 않도록 다음 프레임에 복귀합니다.
            _settingsReturn = StartCoroutine(ReturnFromSettings());
        }

        private IEnumerator ReturnFromSettings()
        {
            yield return null;
            _settingsPending = false;
            _settingsReturn = null;
            _host.CompleteRequest();
            _host.enabled = true;
        }

        private async void Start()
        {
            if (_audioPrefab != null) _audio = Instantiate(_audioPrefab);
            if (_audio != null) { _audio.Apply(GameSettingsStore.Load()); _audio.PlayMusic(_musicKey); }
            try
            {
                string root = Path.GetFullPath(Application.persistentDataPath);
                var records = await new JsonSaveStore(Path.Combine(root, _saveSubdirectory), _saveVersion).ReadAsync();
                if (this != null) _host.RefreshSave(records.Count > 0);
            }
            catch (Exception error)
            {
                // 조회 실패를 저장 없음으로 바꾸지 않습니다. 호스트는 대기 안내를 유지합니다.
                Debug.LogError($"타이틀 저장 조회에 실패했습니다.\n{error.Message}", this);
            }
        }

        private bool StartNewGame()
        {
            if (!Application.CanStreamedLevelBeLoaded(_newGameScene)) return false;
            Cursor.visible = false;
            if (_data == null || _data.Data == null || _loadingPrefab == null) return false;
            HumanGhost.Gameplay.Preview.PreviewSession.Reset();
            Instantiate(_loadingPrefab).Begin(_newGameScene, _data.Data);
            return true;
        }
    }
}
