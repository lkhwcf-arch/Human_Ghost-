using System;
using System.IO;
using System.Collections;
using HumanGhost.Data.Infrastructure;
using HumanGhost.Gameplay.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HumanGhost.Gameplay.Adapters
{
    // 타이틀 저장 유무·새 게임 진입·공용 옵션 화면을 연결합니다. 인트로·이어하기 복원은 소유하지 않습니다.
    public sealed class TitleGameEntry : MonoBehaviour, IModalInputTarget
    {
        [SerializeField] private TitleSceneHost _host = null;
        [SerializeField] private GameDataInitializer _data = null;
        [SerializeField, InspectorName("로딩 화면 프리팹")] private LoadingScreen _loadingPrefab = null;
        [SerializeField, InspectorName("소리 프리팹")] private GameAudio _audioPrefab = null;
        private GameAudio _audio;
        [SerializeField, InspectorName("타이틀 배경음 키")] private string _musicKey = "BGM_VILLAGE";
        [SerializeField] private SettingsScreen _settingsPrefab = null;
        [SerializeField] private InputActionAsset _input = null;
        private SettingsScreen _settingsScreen;
        private MenuInput _settingsInput;
        private GameSettings _settings = new GameSettings();
        private SettingsApplication _settingsApplication;
        private bool _modalBlocked;
        private bool _started;
        private int _saveGeneration;
        private bool _settingsPending;
        private Coroutine _settingsReturn;
        [SerializeField, InspectorName("새 게임 시작 씬")] private string _newGameScene = "MapDohwagolTest";
        [SerializeField, InspectorName("저장 하위 폴더")] private string _saveSubdirectory = "Saves/Demo";
        [SerializeField, InspectorName("지원 저장 버전")] private int _saveVersion = 1;

        public void SetModalBlocked(bool blocked)
        {
            _modalBlocked = blocked;
            _settingsInput?.SetBlocked(blocked);
            if (_settingsScreen != null) _settingsScreen.SetInputBlocked(blocked);
        }

        private void OnEnable()
        {
            if (_host == null) throw new InvalidOperationException("타이틀 실행 담당의 메뉴 호스트를 연결해 주세요.");
            _host.Connect(TitleAction.NewGame, StartNewGame);
            _host.Connect(TitleAction.Settings, OpenSettings);
            _host.Connect(TitleAction.Quit, Quit);
            if (_started) RefreshSave();
        }

        private void OnDisable()
        {
            ++_saveGeneration;
            if (_host != null)
            {
                _host.Disconnect(TitleAction.NewGame, StartNewGame);
                _host.Disconnect(TitleAction.Settings, OpenSettings);
                _host.Disconnect(TitleAction.Quit, Quit);
            }
            _settingsInput?.Disable();
            if (_settingsScreen != null) _settingsScreen.Cancel();
            if (_settingsReturn != null) StopCoroutine(_settingsReturn);
            _settingsReturn = null;
            if (_settingsPending && _host != null)
            {
                _host.CompleteRequest();
                _host.enabled = true;
            }
            _settingsPending = false;
        }

        private bool OpenSettings()
        {
            if (_modalBlocked || _settingsPending) return false;
            if (_host == null) throw new InvalidOperationException("타이틀 설정을 열 수 없습니다. 타이틀 메뉴 호스트가 연결되지 않았습니다.");
            if (_data == null || _data.Data == null) throw new InvalidOperationException("타이틀 설정을 열 수 없습니다. 데이터 초기화가 연결되지 않았거나 완료되지 않았습니다.");
            if (_settingsPrefab == null) throw new InvalidOperationException("타이틀 설정 화면 프리팹이 연결되지 않았습니다. TitleGameEntry의 설정 프리팹을 확인해 주세요.");
            if (_input == null) throw new InvalidOperationException("타이틀 설정 입력 에셋이 연결되지 않았습니다.");
            if (_settingsApplication == null)
                _settingsApplication = new SettingsApplication(PreviewSettings, GameSettingsStore.Save, error => _host.ReportError(error, "타이틀 설정 적용·저장"));
            if (_settingsScreen == null)
            {
                _settingsScreen = Instantiate(_settingsPrefab, transform);
                try
                {
                    _settingsInput = new MenuInput(_input, _settingsScreen.Move, _settingsScreen.Submit, _settingsScreen.Back, _settingsScreen.Adjust);
                }
                catch { Destroy(_settingsScreen.gameObject); _settingsScreen = null; throw; }
            }
            _settingsInput.SetBlocked(_modalBlocked);
            _settingsScreen.SetInputBlocked(_modalBlocked);
            _settingsScreen.Open(_data.Data, _settings, PreviewSettings, OnSettingsClosed);
            try { _settingsInput.Enable(); }
            catch { _settingsScreen.Cancel(); throw; }
            _settingsPending = true;
            _host.enabled = false;
            Cursor.visible = true;
            return true;
        }

        private void PreviewSettings(GameSettings value) => _audio?.Apply(value);

        private void OnSettingsClosed(GameSettings result)
        {
            _settingsInput.Disable();
            try
            {
                if (_settingsApplication.Complete(_settings, result)) _settings = result;
            }
            finally
            {
                // 오류 보고가 실패해도 부모 입력 반환은 실행합니다.
                if (isActiveAndEnabled) _settingsReturn = StartCoroutine(ReturnFromSettings());
            }
        }

        private void OnDestroy() => _settingsInput?.Dispose();

        private IEnumerator ReturnFromSettings()
        {
            yield return null;
            _settingsPending = false;
            _settingsReturn = null;
            _host.CompleteRequest();
            _host.enabled = true;
        }

        private void Start()
        {
            _settings = GameSettingsStore.Load();
            if (_audioPrefab != null) _audio = Instantiate(_audioPrefab);
            if (_audio != null) { _audio.Apply(_settings); _audio.PlayMusic(_musicKey); }
            _started = true;
            RefreshSave();
        }

        private async void RefreshSave()
        {
            int generation = ++_saveGeneration;
            try
            {
                string root = Path.GetFullPath(Application.persistentDataPath);
                var records = await new JsonSaveStore(Path.Combine(root, _saveSubdirectory), _saveVersion).ReadAsync();
                if (this != null && isActiveAndEnabled && generation == _saveGeneration && _host != null) _host.RefreshSave(records.Count > 0);
            }
            catch (Exception error)
            {
                if (this == null || !isActiveAndEnabled || generation != _saveGeneration) return;
                // 조회 실패를 저장 없음으로 바꾸지 않습니다. 호스트는 대기 안내를 유지합니다.
                Debug.LogError($"타이틀 저장 조회에 실패했습니다.\n{error.Message}", this);
            }
        }

        private bool StartNewGame()
        {
            if (!Application.CanStreamedLevelBeLoaded(_newGameScene)) return false;
            if (_data == null || _data.Data == null || _loadingPrefab == null) return false;
            Instantiate(_loadingPrefab).Begin(_newGameScene, _data.Data);
            HumanGhost.Gameplay.Preview.PreviewSession.Reset();
            Cursor.visible = false;
            return true;
        }

        private static bool Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
            return true;
        }
    }
}
