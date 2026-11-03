using System;
using System.Threading.Tasks;
using HumanGhost.Data.Infrastructure;
using HumanGhost.Gameplay.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HumanGhost.Gameplay.Adapters
{
    // 데이터 준비 뒤 메뉴·화면·입력을 조립합니다. 실행 담당은 외부에서 명시적으로 연결합니다.
    public sealed class TitleSceneHost : MonoBehaviour
    {
        [SerializeField] private GameDataInitializer _initializer = null;
        [SerializeField] private TitleScreen _screen = null;
        [SerializeField] private InputActionAsset _input = null;
        [SerializeField, InspectorName("타이틀 메뉴 설정")] private TitleMenuSettings _settings = null;
        [SerializeField, InspectorName("화면 검사 모드 사용")] private bool _previewMode = true;
        [SerializeField, InspectorName("화면 검사: 임시 저장 있음")] private bool _previewHasSave = false;
        private readonly TaskCompletionSource<bool> _saveReady =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private InputActionAsset _ownedInput;
        private InputAction _navigate, _submit, _cancel;
        private TitleMenu _menu;
        private Func<TitleAction, bool> _execute;
        private bool _subscribed;
        private int _vertical;

        // true는 실행 담당이 요청을 인수했음을 뜻합니다. 실패·설정 복귀 후 CompleteRequest를 호출합니다.
        public void ConnectOperations(Func<TitleAction, bool> execute)
        {
            if (execute == null) throw new ArgumentNullException(nameof(execute));
            if (_execute != null && _execute != execute)
                throw new InvalidOperationException("타이틀 실행 담당이 이미 연결되어 있습니다.");
            _execute = execute;
        }

        public void DisconnectOperations(Func<TitleAction, bool> execute)
        {
            if (_execute == execute) _execute = null;
        }
        public void RefreshSave(bool hasSave)
        {
            if (this == null) throw new InvalidOperationException("종료된 타이틀에는 저장 상태를 전달할 수 없습니다.");
            if (_menu != null) _menu.RefreshSave(hasSave);
            _saveReady.TrySetResult(hasSave);
        }
        public void CompleteRequest() => _menu?.CompleteRequest();

        private void OnEnable() { if (_menu != null) Subscribe(); }

        private async void Start()
        {
            if (_menu != null) { Subscribe(); return; }
            try { await InitializeAsync(); }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                if (this != null)
                {
                    Release();
                    if (_screen != null) _screen.ShowStatus("타이틀 준비에 실패했습니다. 콘솔을 확인해 주세요.");
                    Debug.LogError($"타이틀 준비에 실패했습니다.\n{error.Message}", this);
                }
            }
        }

        private async Task InitializeAsync()
        {
            if (_settings == null) throw new InvalidOperationException("타이틀 메뉴 설정 에셋을 연결해 주세요.");
            var data = await _initializer.InitializeAsync();
            if (this == null) return;
            if (!_previewMode && !_saveReady.Task.IsCompleted)
                _screen.ShowStatus("저장 조회 결과를 기다리고 있습니다.");
            bool hasSave = _previewMode ? _previewHasSave : await _saveReady.Task;
            if (this == null) return;
            _menu = new TitleMenu(hasSave, _settings.ShowQuit, _settings.ConfirmNewGameWithSave, _settings.PreferContinue);
            _screen.Connect(_menu, data);
            _ownedInput = Instantiate(_input);
            _ownedInput.Disable();
            _navigate = _ownedInput.FindAction("UI/Navigate", true);
            _submit = _ownedInput.FindAction("UI/Submit", true);
            _submit.AddBinding("<Keyboard>/c");
            _cancel = _ownedInput.FindAction("UI/Cancel", true);
            _screen.ShowStatus(_previewMode ? "화면 검사 모드입니다. 실제 저장 조회는 사용하지 않습니다." : "");
            if (isActiveAndEnabled) Subscribe();
        }

        private void Subscribe()
        {
            if (_subscribed || _ownedInput == null) return;
            _subscribed = true;
            _navigate.performed += OnNavigate;
            _navigate.canceled += OnNavigate;
            _submit.performed += OnSubmit;
            _cancel.performed += OnCancel;
            _navigate.Enable(); _submit.Enable(); _cancel.Enable();
        }

        private void OnDisable()
        {
            if (!_subscribed) return;
            _subscribed = false;
            _navigate.performed -= OnNavigate;
            _navigate.canceled -= OnNavigate;
            _submit.performed -= OnSubmit;
            _cancel.performed -= OnCancel;
            _ownedInput.Disable();
            _vertical = 0;
        }

        private void OnNavigate(InputAction.CallbackContext context)
        {
            float value = context.ReadValue<Vector2>().y;
            int direction = value > 0.5f ? -1 : value < -0.5f ? 1 : 0;
            if (direction != 0 && direction != _vertical) _menu.Move(direction);
            _vertical = direction;
        }

        private void OnSubmit(InputAction.CallbackContext context) => Dispatch(
            _menu.State == TitleMenuState.ConfirmingNewGame ? _menu.AnswerNewGame(true) : _menu.Submit());

        private void OnCancel(InputAction.CallbackContext context)
        {
            if (_menu.State == TitleMenuState.ConfirmingNewGame) _menu.AnswerNewGame(false);
            else _menu.Back();
        }

        private void Dispatch(TitleAction? request)
        {
            if (!request.HasValue) return;
            try
            {
                if (_execute != null && _execute(request.Value)) return;
                _screen.ShowStatus("선택한 기능의 실행 담당이 아직 연결되지 않았습니다.");
            }
            catch (Exception error)
            {
                _screen.ShowStatus("기능 실행에 실패했습니다. 콘솔을 확인해 주세요.");
                Debug.LogError($"타이틀 요청 처리에 실패했습니다.\n{error.Message}", this);
            }
            _menu.CompleteRequest();
        }

        private void Release()
        {
            OnDisable();
            if (_screen != null) _screen.Disconnect();
            if (_ownedInput != null) Destroy(_ownedInput);
            _ownedInput = null;
            _menu = null;
        }

        private void OnDestroy()
        {
            _saveReady.TrySetCanceled();
            Release();
            _execute = null;
        }
    }
}
