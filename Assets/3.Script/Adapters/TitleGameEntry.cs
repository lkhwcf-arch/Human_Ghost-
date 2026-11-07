using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using HumanGhost.Data.Infrastructure;
using HumanGhost.Data.Storage;
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
        private IReadOnlyList<SaveRecord> _records;
        private JsonSaveStore _saveStore;
        private bool _settingsPending;
        private Coroutine _settingsReturn;
        [SerializeField, InspectorName("새 게임 시작 씬")] private string _newGameScene = "MapDohwagolTest";
        [SerializeField, InspectorName("저장 하위 폴더")] private string _saveSubdirectory = "Saves/Demo";
        [SerializeField, InspectorName("지원 저장 버전")] private int _saveVersion = 1;
        [SerializeField, InspectorName("공용 컷씬 실행 담당")] private CutsceneHost _cutscenes = null;
        private int _introCutsceneId;
        private bool _introPending;

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
            _host.Connect(TitleAction.Continue, ContinueGame);
            _host.ConnectSaveLoadHandler(LoadRecord);
            _host.Connect(TitleAction.Settings, OpenSettings);
            _host.Connect(TitleAction.Quit, Quit);
            if (_data != null) _data.Initialized += ConnectAudioData;
            if (_data != null && _data.Data != null) ConnectAudioData(_data.Data);
            if (_started) RefreshSave();
        }

        private void OnDisable()
        {
            if (_introPending)
            {
                _cutscenes?.Cancel();
                if (_cutscenes != null) _cutscenes.TryTakeResult(out _);
                _introPending = false;
                _host?.CompleteRequest();
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

        private void ConnectAudioData(IReadOnlyDataCatalog data)
        {
            if (_audio != null) _audio.ConnectTable(data);
        }

        private bool StartNewGame()
        {
            if (_data == null || _data.Data == null) return false;
            _introCutsceneId = CutsceneSequenceSource.FindId(_data.Data, CutsceneSequenceSource.IntroKey);
            if (_introCutsceneId == 0) return EnterGame(null);
            if (_modalBlocked || _introPending || _records == null || _saveStore == null) return false;
            if (_cutscenes == null || _data == null || _data.Data == null)
                throw new InvalidOperationException("인트로의 공용 실행 담당과 초기화가 완료된 데이터를 연결해 주세요.");
            if (!_cutscenes.TryPlay(_data.Data, _introCutsceneId, _audio)) return false;
            _introPending = true;
            return true;
        }

        private void LateUpdate()
        {
            if (!_introPending) return;
            CutsceneEndReason reason = CutsceneEndReason.Cancelled;
            if (_cutscenes != null && !_cutscenes.TryTakeResult(out reason)) return;
            _introPending = false;
            try
            {
                if (!reason.AllowsProgress() || !EnterGame(null)) _host.CompleteRequest();
                else GameSaveContext.Current.Progress.MarkCutsceneViewed(_introCutsceneId);
            }
            catch (Exception error) { _host.CompleteRequest(); _host.ReportError(error, "인트로 종료 후 새 게임 진입"); }
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
            var context = GameSaveContext.Create(records, _saveStore, _saveVersion, record);
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
