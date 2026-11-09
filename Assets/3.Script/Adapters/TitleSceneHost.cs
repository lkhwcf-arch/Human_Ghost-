using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HumanGhost.Data.Infrastructure;
using HumanGhost.Gameplay.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HumanGhost.Gameplay.Adapters
{
    // 데이터 준비 뒤 메뉴·화면·입력을 조립합니다. 실행 담당은 외부에서 명시적으로 연결합니다.
    public sealed class TitleSceneHost : MonoBehaviour, IModalInputTarget
    {
        [SerializeField] private GameDataInitializer _initializer = null;
        [SerializeField] private TitleScreen _screen = null;
        [SerializeField, InspectorName("에러 팝업 연결")] private ErrorPopupSceneBinding _errors = null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [SerializeField] private TitleSaveSelection _saveSelection = null;
#endif
        [SerializeField] private InputActionAsset _input = null;
        [SerializeField, InspectorName("타이틀 메뉴 설정")] private TitleMenuSettings _settings = null;
        [SerializeField, InspectorName("화면 검사 모드 사용")] private bool _previewMode = true;
        [SerializeField, InspectorName("화면 검사: 임시 저장 있음")] private bool _previewHasSave = false;
        private readonly TaskCompletionSource<bool> _saveReady =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _modalBlocked;
        private bool _started;
        private int _generation;
        public void SetModalBlocked(bool blocked)
        {
            _modalBlocked = blocked;
            _menuInput?.SetBlocked(blocked);
        }
        private MenuInput _menuInput;
        private TitleMenu _menu;
        private readonly Dictionary<TitleAction, Func<bool>> _commands = new Dictionary<TitleAction, Func<bool>>();
        public void ConnectSaveLoadHandler(Func<SaveRecord, bool> handler)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_saveSelection != null) _saveSelection.ConnectLoadHandler(handler);
#endif
        }
        public void DisconnectSaveLoadHandler(Func<SaveRecord, bool> handler)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_saveSelection != null) _saveSelection.DisconnectLoadHandler(handler);
#endif
        }
        public bool CanOpenSaveSelection => !_modalBlocked && _menu != null && _menu.State == TitleMenuState.Selecting;

        public void ReportError(Exception error, string state)
        {
            if (_errors != null) _errors.Report(error, this, state);
            else Debug.LogException(error, this);
        }

        // true는 실행 담당이 요청을 인수했음을 뜻합니다. 실패·설정 복귀 후 CompleteRequest를 호출합니다.
        public void Connect(TitleAction action, Func<bool> execute)
        {
            if (execute == null) throw new ArgumentNullException(nameof(execute));
            if (!Enum.IsDefined(typeof(TitleAction), action)) throw new ArgumentOutOfRangeException(nameof(action));
            if (_commands.ContainsKey(action)) throw new InvalidOperationException("이미 실행 담당이 연결된 타이틀 요청입니다: " + action);
            _commands.Add(action, execute);
        }

        public void Disconnect(TitleAction action, Func<bool> execute)
        {
            if (_commands.TryGetValue(action, out Func<bool> current) && current == execute) _commands.Remove(action);
        }
        public void RefreshSave(bool hasSave)
        {
            if (this == null) throw new InvalidOperationException("종료된 타이틀에는 저장 상태를 전달할 수 없습니다.");
            if (_menu != null) _menu.RefreshSave(hasSave);
            _saveReady.TrySetResult(hasSave);
        }
        public void CompleteRequest() => _menu?.CompleteRequest();

        private void OnEnable() { if (_menu != null) Subscribe(); else if (_started) Prepare(); }

        // 마지막 입력 장치 기준 마우스 화살표 표시입니다. 메뉴 선택은 키보드와 하나를 같이 씁니다.
        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.delta.ReadValue().sqrMagnitude > 0f) Cursor.visible = true;
        }

        private void Start() { _started = true; Prepare(); }

        private bool IsCurrent(int generation) => this != null && isActiveAndEnabled && _generation == generation;

        private async void Prepare()
        {
            int generation = ++_generation;
            if (_menu != null) { Subscribe(); return; }
            try { await InitializeAsync(generation); }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                if (IsCurrent(generation))
                {
                    Release();
                    if (_screen != null) _screen.ShowStatus("타이틀 준비에 실패했습니다. 콘솔을 확인해 주세요.");
                    Debug.LogError($"타이틀 준비에 실패했습니다.\n{error.Message}", this);
                }
            }
        }

        private async Task InitializeAsync(int generation)
        {
            if (_settings == null) throw new InvalidOperationException("타이틀 메뉴 설정 에셋을 연결해 주세요.");
            var data = await _initializer.InitializeAsync();
            if (!IsCurrent(generation)) return;
            if (!_previewMode && !_saveReady.Task.IsCompleted)
                _screen.ShowStatus("저장 조회 결과를 기다리고 있습니다.");
            bool hasSave = _previewMode ? _previewHasSave : await _saveReady.Task;
            if (!IsCurrent(generation)) return;
            _menu = new TitleMenu(hasSave, _settings.ShowQuit, _settings.ConfirmNewGameWithSave, _settings.PreferContinue,
                _screen.ReadConfirmDefaultYes(data));
            _screen.Connect(_menu, data);
            _menuInput = new MenuInput(_input, OnKeyMove, OnKeySubmit, OnKeyCancel);
            _screen.EntryHovered += OnEntryHovered;
            _screen.EntryClicked += OnEntryClicked;
            _screen.ConfirmHovered += OnConfirmHovered;
            _screen.ConfirmClicked += OnConfirmClicked;
            _screen.CloseClicked += OnCancel;
            Cursor.visible = true;
            _screen.ShowStatus(_previewMode ? "화면 검사 모드입니다. 실제 저장 조회는 사용하지 않습니다." : "");
            if (isActiveAndEnabled) Subscribe();
        }

        private void Subscribe()
        {
            _menuInput?.SetBlocked(_modalBlocked);
            _menuInput?.Enable();
        }

        private void OnDisable()
        {
            ++_generation;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_saveSelection != null) _saveSelection.Close();
#endif
            _menuInput?.Disable();
        }

        private void OnKeyMove(int direction) { if (_modalBlocked || !isActiveAndEnabled) return; Cursor.visible = false; OnMove(direction); }
        private void OnKeySubmit() { if (_modalBlocked || !isActiveAndEnabled) return; Cursor.visible = false; OnSubmit(); }
        private void OnKeyCancel() { if (_modalBlocked || !isActiveAndEnabled) return; Cursor.visible = false; OnCancel(); }
        private bool IsPointerBlocked()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_saveSelection != null && _saveSelection.IsOpen) return true;
#endif
            return !isActiveAndEnabled || _modalBlocked || _menu == null;
        }
        private void OnEntryHovered(TitleAction action) { if (!IsPointerBlocked()) _menu.SelectItem(action); }
        private void OnEntryClicked(TitleAction action)
        {
            if (IsPointerBlocked() || _menu.State != TitleMenuState.Selecting) return;
            KeyboardControls.NoteDevice(Mouse.current);
            _menu.SelectItem(action);
            Dispatch(_menu.Submit());
        }
        private void OnConfirmHovered(bool yes) { if (!IsPointerBlocked()) _menu.SelectConfirm(yes); }
        private void OnConfirmClicked(bool yes)
        {
            if (IsPointerBlocked()) return;
            KeyboardControls.NoteDevice(Mouse.current);
            Dispatch(_menu.AnswerNewGame(yes));
        }

        private void OnMove(int direction)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_saveSelection != null && _saveSelection.IsOpen) _saveSelection.Move(direction);
            else
#endif
                _menu.Move(direction);
        }

        private void OnSubmit()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_saveSelection != null && _saveSelection.IsOpen) _saveSelection.Submit();
            else
#endif
                Dispatch(_menu.State == TitleMenuState.ConfirmingNewGame ? _menu.AnswerNewGame(_menu.ConfirmYes) : _menu.Submit());
        }

        private void OnCancel()
        {
            if (_modalBlocked) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_saveSelection != null && _saveSelection.IsOpen) { _saveSelection.Close(); return; }
#endif
            if (_menu.State == TitleMenuState.ConfirmingNewGame) _menu.AnswerNewGame(false);
            else _menu.Back();
        }

        private void Dispatch(TitleAction? request)
        {
            if (!request.HasValue) return;
            try
            {
                if (_commands.TryGetValue(request.Value, out Func<bool> execute) && execute()) return;
                throw new InvalidOperationException("선택한 타이틀 기능의 실행 담당이 없거나 요청을 처리하지 못했습니다. 요청: " + request.Value);
            }
            catch (Exception error)
            {
                if (_screen != null) _screen.ShowStatus("기능 실행에 실패했습니다. 콘솔을 확인해 주세요.");
                _menu?.CompleteRequest();
                ReportError(error, "타이틀 요청: " + request.Value);
                return;
            }
        }

        private void Release()
        {
            OnDisable();
            if (_screen != null)
            {
                _screen.EntryHovered -= OnEntryHovered;
                _screen.EntryClicked -= OnEntryClicked;
                _screen.ConfirmHovered -= OnConfirmHovered;
                _screen.ConfirmClicked -= OnConfirmClicked;
                _screen.CloseClicked -= OnCancel;
                _screen.Disconnect();
            }
            _menuInput?.Dispose();
            _menuInput = null;
            _menu = null;
        }

        private void OnDestroy()
        {
            _saveReady.TrySetCanceled();
            Release();
            _commands.Clear();
        }
    }
}
