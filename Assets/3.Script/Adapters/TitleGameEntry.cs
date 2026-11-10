using System;
using System.Collections.Generic;
using System.Linq;
using HumanGhost.Data.Infrastructure;
using HumanGhost.Data.Storage;
using HumanGhost.Gameplay.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HumanGhost.Gameplay.Adapters
{
    // 타이틀 요청을 키 선택·인트로·저장 진입에 연결합니다. 재생·저장 규칙은 기존 모듈을 사용합니다.
    public sealed partial class TitleGameEntry : MonoBehaviour, IModalInputTarget
    {
        [SerializeField] private TitleSceneHost _host = null;
        [SerializeField] private GameDataInitializer _data = null;
        [SerializeField, InspectorName("로딩 화면 프리팹")] private LoadingScreen _loadingPrefab = null;
        [SerializeField, InspectorName("소리 프리팹")] private GameAudio _audioPrefab = null;
        private GameAudio _audio;
        [SerializeField, InspectorName("타이틀 배경 영상")] private TitleVideoBackground _backgroundVideo = null;
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
        private IReadOnlyList<SaveRecord> _records;
        private JsonSaveStore _saveStore;
        private bool _menuPending;
        private int _menuReturnFrame = -1;
        [SerializeField, InspectorName("새 게임 시작 씬")] private string _newGameScene = "MapDohwagolTest";
        [SerializeField, InspectorName("저장 하위 폴더")] private string _saveSubdirectory = "Saves/Demo";
        [SerializeField, InspectorName("지원 저장 버전")] private int _saveVersion = 1;
        [SerializeField, InspectorName("공용 컷씬 실행 담당")] private CutsceneHost _cutscenes = null;
        private GameObject _menuRoot;

        public void SetModalBlocked(bool blocked)
        {
            _modalBlocked = blocked;
            _settingsInput?.SetBlocked(blocked);
            if (_layoutScreen != null) _layoutScreen.SetBlocked(blocked);
            if (_settingsScreen != null) _settingsScreen.SetInputBlocked(blocked);
        }

        private void OnEnable()
        {
            if (_host == null) throw new InvalidOperationException("타이틀 실행 담당의 메뉴 호스트를 연결해 주세요.");
            _host.Connect(TitleAction.NewGame, StartNewGame);
            _host.Connect(TitleAction.Continue, ContinueGame);
            _host.ConnectSaveLoadHandler(LoadRecord);
            _host.Connect(TitleAction.Settings, OpenSettings);
            _host.Connect(TitleAction.Quit, Quit);
            if (_data != null) _data.Initialized += ConnectAudioData;
            if (_data != null && _data.Data != null) ConnectAudioData(_data.Data);
            if (_backgroundVideo != null) _backgroundVideo.Ready += PlayTitleMusic;
            if (_started)
            {
                if (_backgroundVideo == null || _backgroundVideo.IsReady) PlayTitleMusic();
                RefreshSave();
            }
        }

        private void OnDisable()
        {
            if (_backgroundVideo != null) _backgroundVideo.Ready -= PlayTitleMusic;
            if (_audio != null) _audio.StopMusic();
            _layoutResultReady = false;
            _layoutPending = false;
            _layoutResult = null;
            if (_layoutScreen != null) _layoutScreen.Cancel();
            if (_introPending)
            {
                _cutscenes?.Cancel();
                if (_cutscenes != null) _cutscenes.TryTakeResult(out _);
                _introPending = false;
            }
            ++_saveGeneration;
            if (_data != null) _data.Initialized -= ConnectAudioData;
            if (_host != null)
            {
                _host.Disconnect(TitleAction.NewGame, StartNewGame);
                _host.Disconnect(TitleAction.Continue, ContinueGame);
                _host.DisconnectSaveLoadHandler(LoadRecord);
                _host.Disconnect(TitleAction.Settings, OpenSettings);
                _host.Disconnect(TitleAction.Quit, Quit);
            }
            _settingsInput?.Disable();
            if (_settingsScreen != null) _settingsScreen.Cancel();
            _menuReturnFrame = -1;
            if (_menuPending && _host != null)
            {
                _host.CompleteRequest();
                _host.enabled = true;
            }
            _menuPending = false;
        }

        private bool OpenSettings()
        {
            if (_modalBlocked || _menuPending) return false;
            PrepareMenu();
            _settingsInput.SetBlocked(_modalBlocked);
            _settingsScreen.SetInputBlocked(_modalBlocked);
            _settingsScreen.Open(_data.Data, _settings, PreviewSettings, OnSettingsClosed, allowWindowMode: true);
            try { _settingsInput.Enable(); }
            catch { _settingsScreen.Cancel(); throw; }
            _menuPending = true;
            _host.enabled = false;
            Cursor.visible = true;
            return true;
        }

        // 옵션과 새 게임 선택이 같은 입력 사본·설정 적용/저장 경계를 공유합니다.
        private void PrepareMenu()
        {
            if (_host == null || _data == null || _data.Data == null || _settingsPrefab == null || _input == null)
                throw new InvalidOperationException("타이틀 화면·데이터·공용 설정 프리팹·입력 참조를 연결해 주세요.");
            if (_settingsApplication == null)
                _settingsApplication = new SettingsApplication(PreviewSettings, GameSettingsStore.Save,
                    error => _host.ReportError(error, "타이틀 설정 적용·저장"), KeyboardControls.SetLayout);
            if (_menuRoot == null)
            {
                _settingsInput?.Dispose(); _settingsInput = null;
                _menuRoot = new GameObject("TitleMenuScreens", typeof(RectTransform));
                _menuRoot.transform.SetParent(transform, false);
                // 중간 UI 부모도 표시 영역을 가져야 하위 화면의 늘림 앵커가 유효합니다.
                var menuRect = (RectTransform)_menuRoot.transform;
                menuRect.anchorMin = Vector2.zero;
                menuRect.anchorMax = Vector2.one;
                menuRect.offsetMin = Vector2.zero;
                menuRect.offsetMax = Vector2.zero;
                _menuRoot.SetActive(false);
                try
                {
                    _settingsScreen = Instantiate(_settingsPrefab, _menuRoot.transform);
                    _settingsScreen.gameObject.SetActive(false);
                    _settingsInput = new MenuInput(_input, MoveMenu, SubmitMenu, BackMenu, AdjustMenu, acceptBothJumpKeys: true);
                    _menuRoot.SetActive(true);
                }
                catch
                {
                    _settingsInput?.Dispose(); _settingsInput = null;
                    Destroy(_menuRoot); _menuRoot = null; _settingsScreen = null;
                    throw;
                }
            }
        }
        private void MoveMenu(int direction) { if (_layoutPending) _layoutScreen.Move(direction); else _settingsScreen.Move(direction); }
        private void SubmitMenu() { if (_layoutPending) _layoutScreen.Submit(); else _settingsScreen.Submit(); }
        private void BackMenu() { if (_layoutPending) _layoutScreen.Back(); else _settingsScreen.Back(); }
        private void AdjustMenu(int direction) { if (_layoutPending) _layoutScreen.Move(direction); else _settingsScreen.Adjust(direction); }

        private void PreviewSettings(GameSettings value)
        {
            WindowModeApplication.Apply(value.WindowMode);
            _audio?.Apply(value);
        }

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
                if (isActiveAndEnabled) QueueMenuReturn();
            }
        }

        private void OnDestroy() => _settingsInput?.Dispose();

        private void QueueMenuReturn() => _menuReturnFrame = Time.frameCount;

        private void ReturnFromMenu()
        {
            _menuPending = false;
            _menuReturnFrame = -1;
            _host.CompleteRequest();
            _host.enabled = true;
        }

        private void Start()
        {
            _settings = GameSettingsStore.Load();
            if (_audioPrefab != null) _audio = Instantiate(_audioPrefab, transform);
            if (_audio != null) _audio.Apply(_settings);
            if (_backgroundVideo == null || _backgroundVideo.IsReady) PlayTitleMusic();
            if (_data != null && _data.Data != null) ConnectAudioData(_data.Data);
            _started = true;
            RefreshSave();
        }

        private async void RefreshSave()
        {
            int generation = ++_saveGeneration;
            try
            {
                var store = new JsonSaveStore(GameSaveContext.DirectoryPath(_saveSubdirectory), _saveVersion);
                var records = await store.ReadAsync();
                if (this == null || !isActiveAndEnabled || generation != _saveGeneration || _host == null) return;
                _saveStore = store;
                _records = records;
                _host.RefreshSave(records.Count > 0);
            }
            catch (Exception error)
            {
                if (this == null || !isActiveAndEnabled || generation != _saveGeneration) return;
                // 조회 실패를 저장 없음으로 바꾸지 않습니다. 호스트는 대기 안내를 유지합니다.
                Debug.LogError($"타이틀 저장 조회에 실패했습니다.\n{error.Message}", this);
            }
        }

        private void PlayTitleMusic() { if (_audio != null) _audio.PlayMusic(_musicKey); }

        private void ConnectAudioData(IReadOnlyDataCatalog data)
        {
            if (_audio != null) _audio.ConnectTable(data);
        }

        private bool ContinueGame()
        {
            SaveRecord record = _records == null ? null : SaveRecord.LatestFirst(_records).FirstOrDefault();
            return record != null && EnterGame(record);
        }
        private bool LoadRecord(SaveRecord record) => EnterGame(record);
        private bool EnterGame(SaveRecord record)
        {
            if (_modalBlocked || _records == null || _saveStore == null) return false;
            if (_data == null || _data.Data == null || _loadingPrefab == null) return false;
            var records = record == null ? _records : _records.Where(row => row.Slot != record.Slot).Append(record).ToArray();
            var gates = _data.Data.GetTable<HumanGhost.Data.Definitions.GateConfig>();
            var context = GameSaveContext.Create(records, _saveStore, _saveVersion, id => gates.TryGet(id, out _), record);
            record = context.Entry;
            if (record != null)
            {
                if (record.Snapshot.Hp <= 0) throw new InvalidOperationException("사망 상태의 저장 기록으로는 이어하기를 시작할 수 없습니다.");
                _data.Data.GetTable<HumanGhost.Data.Definitions.Stage>().Get(record.Snapshot.StageId);
                foreach (int id in record.Snapshot.OpenedGates) _data.Data.GetTable<HumanGhost.Data.Definitions.GateConfig>().Get(id);
            }
            Instantiate(_loadingPrefab).Begin(_newGameScene, _data.Data, context.SceneReady);
            GameSaveContext.Set(context);
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
