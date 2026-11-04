using System;
using System.IO;
using HumanGhost.Data.Infrastructure;
using HumanGhost.Gameplay.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HumanGhost.Gameplay.Adapters
{
    // 타이틀의 저장 유무 전달과 새 게임 로딩 화면 진입만 연결합니다. 인트로·이어하기 복원은 소유하지 않습니다.
    public sealed class TitleGameEntry : MonoBehaviour
    {
        [SerializeField] private TitleSceneHost _host = null;
        [SerializeField] private TitleSceneActions _actions = null;
        [SerializeField] private GameDataInitializer _data = null;
        [SerializeField, InspectorName("로딩 화면 프리팹")] private LoadingScreen _loadingPrefab = null;
        [SerializeField, InspectorName("설정 화면")] private SettingsScreen _settingsPrefab = null;
        [SerializeField, InspectorName("소리 프리팹")] private GameAudio _audioPrefab = null;
        private SettingsScreen _settingsScreen;
        private GameAudio _audio;
        [SerializeField, InspectorName("입력 에셋")] private InputActionAsset _input = null;
        [SerializeField, InspectorName("타이틀 배경음 키")] private string _musicKey = "BGM_VILLAGE";
        private MenuInput _settingsInput;
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
            _settingsInput?.Dispose();
            _settingsInput = null;
        }

        private bool OpenSettings()
        {
            if (_data == null || _data.Data == null || _settingsScreen == null) return false;
            if (_settingsInput == null)
                _settingsInput = new MenuInput(_input, _settingsScreen.Move, _settingsScreen.Submit, _settingsScreen.Back, _settingsScreen.Adjust);
            _settingsScreen.Open(_data.Data, GameSettingsStore.Load(), value => { if (_audio != null) _audio.Apply(value); }, OnSettingsClosed);
            _settingsInput.Enable();
            return true;
        }

        private void OnSettingsClosed(GameSettings result)
        {
            GameSettingsStore.Save(result);
            if (_audio != null) _audio.Apply(result);
            _settingsInput.Disable();
            _host.CompleteRequest();
        }

        private async void Start()
        {
            if (_settingsPrefab != null) { _settingsScreen = Instantiate(_settingsPrefab, transform.root); _settingsScreen.gameObject.SetActive(false); }
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
