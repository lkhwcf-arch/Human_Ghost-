#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using HumanGhost.Gameplay.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace HumanGhost.Gameplay.Adapters
{
    // 타이틀은 파일 조회와 실행 담당 연결만 소유하며 목록·확인·날짜 표시는 공통 화면에 맡깁니다.
    public sealed class TitleSaveSelection : MonoBehaviour
    {
        [SerializeField, InspectorName("테스트 저장 선택 사용")] private bool _enableForTests = true;
        [SerializeField] private TitleSceneHost _host = null;
        [SerializeField] private Button _openButton = null;
        [SerializeField] private SaveSlotScreen _screen = null;
        [SerializeField, InspectorName("테스트 저장 하위 폴더")] private string _saveSubdirectory = "Saves/Demo";
        [SerializeField, InspectorName("지원 저장 버전")] private int _saveVersion = 1;
        private Func<SaveRecord, bool> _load;
        private JsonSaveStore _store;
        private SaveSession _session;
        private bool _busy;
        private int _generation;
        public bool IsOpen { get; private set; }

        public void ConnectLoadHandler(Func<SaveRecord, bool> load)
        {
            if (load == null) throw new ArgumentNullException(nameof(load));
            if (_load != null && _load != load) throw new InvalidOperationException("저장 기록 실행 담당이 이미 연결되어 있습니다.");
            _load = load;
        }
        public void DisconnectLoadHandler(Func<SaveRecord, bool> load) { if (_load == load) _load = null; }
        private void Awake()
        {
            _screen.Hide();
            _openButton.gameObject.SetActive(_enableForTests);
        }
        private void OnEnable()
        {
            _openButton.onClick.AddListener(Open);
            _screen.SelectionRequested += Select;
            _screen.CloseRequested += Close;
        }
        private void OnDisable()
        {
            _openButton.onClick.RemoveListener(Open);
            _screen.SelectionRequested -= Select;
            _screen.CloseRequested -= Close;
            Close();
        }

        public async void Open()
        {
            if (!_enableForTests || !isActiveAndEnabled || IsOpen || !_host.CanOpenSaveSelection) return;
            IsOpen = true;
            _busy = true;
            int generation = ++_generation;
            try
            {
                _screen.Show("저장 기록 선택 · 테스트 전용", Array.Empty<SaveSlotEntry>(), "", "저장 기록을 확인하고 있습니다.");
                _screen.SetBusy(true, true);
                if (_store == null) _store = new JsonSaveStore(GetSaveDirectory(), _saveVersion);
                var records = await _store.ReadAsync();
                if (!IsCurrent(generation)) return;
                _session = new SaveSession(SaveBuildMode.Demo, _saveVersion, _store,
                    () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), records);
                var slots = _session.GetSlots(false);
                _screen.Show("저장 기록 선택 · 테스트 전용", slots, "", slots.Count == 0
                    ? "저장된 기록이 없습니다." : "날짜를 선택해 불러옵니다. 위·아래 이동 / Enter·C 결정 / Esc 닫기");
            }
            catch (Exception error)
            {
                if (!IsCurrent(generation)) return;
                _screen.ShowStatus("저장 기록 조회에 실패했습니다. 원본 파일은 유지됩니다. 콘솔을 확인해 주세요.");
                Debug.LogError($"타이틀 저장 기록 조회에 실패했습니다.\n{error.Message}", this);
            }
            finally { if (IsCurrent(generation)) { _busy = false; _screen.SetBusy(false); } }
        }

        public void Move(int direction) { if (IsOpen) _screen.Move(direction); }
        public void Submit() { if (IsOpen) _screen.Submit(); }
        private void Select(int index)
        {
            if (!IsOpen || _busy) return;
            if (_load == null) { _screen.ShowStatus("실제 맵 복원 담당은 아직 연결되지 않았습니다."); return; }
            _busy = true;
            try
            {
                _screen.SetBusy(true);
                if (_load(_session.GetSlots(false)[index].Record)) { if (this != null) Close(); }
                else _screen.ShowStatus("선택한 기록의 불러오기 요청을 실행하지 못했습니다.");
            }
            catch (Exception error)
            {
                if (this == null) return;
                _screen.ShowStatus("기록 복원 요청에 실패했습니다. 콘솔을 확인해 주세요.");
                Debug.LogError($"타이틀 저장 기록 실행에 실패했습니다.\n{error.Message}", this);
            }
            finally { if (this != null) { _busy = false; if (IsOpen) _screen.SetBusy(false); } }
        }
        public void Close()
        {
            ++_generation;
            IsOpen = false;
            _busy = false;
            if (_screen != null) _screen.Hide();
        }
#if UNITY_EDITOR
        public void StripFromReleaseBuild()
        {
            // 정규 메뉴 마우스가 쓰는 이벤트 시스템·레이캐스터는 출시 빌드에도 남깁니다.
            GameObject button = _openButton.gameObject, panel = _screen.gameObject;
            DestroyImmediate(this);
            DestroyImmediate(button); DestroyImmediate(panel);
        }
#endif
        private bool IsCurrent(int generation) => this != null && isActiveAndEnabled && IsOpen && _generation == generation;
        private string GetSaveDirectory()
        {
            string root = Path.GetFullPath(Application.persistentDataPath) + Path.DirectorySeparatorChar;
            string directory = Path.GetFullPath(Path.Combine(root, _saveSubdirectory));
            if (Path.IsPathRooted(_saveSubdirectory) || !directory.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("테스트 저장 폴더는 게임 저장 경로 내부의 하위 폴더여야 합니다.");
            return directory;
        }
    }
}
#endif
