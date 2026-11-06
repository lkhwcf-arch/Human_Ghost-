using System;
using System.Collections;
using HumanGhost.Gameplay.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using HumanGhost.Data.Storage;
using HumanGhost.Data.Definitions;

namespace HumanGhost.Gameplay.Adapters
{
    // 명시적 보고와 초기화 뒤 수집한 Unity 오류를 같은 화면 진입점으로 처리합니다.
    public class ErrorPopupHost : MonoBehaviour
    {
        [SerializeField] private ErrorPopupScreen _prefab = null;
        [SerializeField] private InputActionAsset _input = null;
        private ErrorPopupScreen _screen;
        private EventSystem _events;
        private MenuInput _menuInput;
        private Action<bool> _setModalInput;
        private Action _dismissed;
        private Coroutine _return;
        private bool _ownsInput;
        [SerializeField, InspectorName("에러 알림 팝업 ID")] private int _popupId = 15007;
        private Popup _popup;
        private string _heading, _copyText, _closeText, _copiedText, _logLabel;
        private string _details;
        private int _errorCount;
        private Coroutine _copyFeedback;
        private int _openedFrame;
        [SerializeField, InspectorName("테스트 환경 Unity 오류 자동 수집")] private bool _captureUnityErrors = true;
        private UnityErrorLogCollector _collector;
        private Func<string> _readState;
        [ThreadStatic] private static int _reportDepth;
        internal static bool IsWritingReport => _reportDepth > 0;
        public bool IsOpen => _ownsInput;
        internal InputActionAsset Input => _input;
        public bool IsCapturing => _collector != null && _collector.IsActive;

        // 콜백은 이 팝업의 입력 잠금만 소유합니다. 실패한 작업의 재시도·게임 시간은 호출자가 결정합니다.
        public virtual void Initialize(EventSystem events, Action<bool> setModalInput, IReadOnlyDataCatalog data, LanguageCode languageCode = LanguageCode.Korean, Func<string> readState = null)
        {
            if (_menuInput != null) throw new InvalidOperationException("이미 초기화된 에러 팝업입니다.");
            if (events == null || setModalInput == null) throw new ArgumentNullException(nameof(events));
            if (_prefab == null || _input == null) throw new InvalidOperationException("에러 팝업 프리팹과 입력 에셋을 연결해 주세요.");
            Configure(data, languageCode);
            _events = events;
            _setModalInput = setModalInput;
            _menuInput = new MenuInput(_input, Move, Submit, Cancel);
            _readState = readState;
            BeginCapture();
        }

        // Popup의 버튼·본문 참조와 공용 언어 조회를 한 번 연결합니다.
        public virtual void Configure(IReadOnlyDataCatalog data, LanguageCode languageCode = LanguageCode.Korean)
        {
            if (_ownsInput) throw new InvalidOperationException("열린 에러 팝업은 닫은 뒤 언어를 변경해 주세요.");
            if (data == null) throw new ArgumentNullException(nameof(data));
            Popup popup = data.GetTable<Popup>().Get(_popupId);
            if (popup.Button2TextID == 0 || popup.Button3TextID != 0 || popup.DefaultButton < 1 || popup.DefaultButton > 2 || popup.CancelButton < 0 || popup.CancelButton > 2)
                throw new InvalidOperationException("에러 알림은 복사·닫기 두 버튼 구성이 필요합니다.");
            var text = new LanguageTextSource(data);
            string heading = text.Read(popup.BodyTextID, languageCode);
            string copy = text.Read(popup.Button1TextID, languageCode);
            string close = text.Read(popup.Button2TextID, languageCode);
            string copied = text.Read(30214, languageCode);
            string logLabel = text.Read(30212, languageCode);
            _popup = popup;
            _heading = heading; _copyText = copy; _closeText = close; _copiedText = copied; _logLabel = logLabel;
        }

        // true는 새 표시 인수, false는 이미 열린 팝업이 있어 로그만 남겼다는 뜻입니다.
        public virtual bool Report(Exception error, Action dismissed = null, UnityEngine.Object context = null, string state = "")
        {
            if (error == null) throw new ArgumentNullException(nameof(error));
            _reportDepth++;
            try { Debug.LogException(error, context); }
            finally { _reportDepth--; }
            _errorCount++;
            if (_popup == null) throw new InvalidOperationException("Popup·Language 데이터를 먼저 연결해 주세요.");
            _details = ErrorPopupDetails.Format(error, _errorCount, ResolveScene(context), state, DateTimeOffset.Now, _logLabel, Application.consoleLogPath);
            return Present(dismissed);
        }

        private bool Present(Action dismissed)
        {
            if (_ownsInput)
            {
                if (_return == null) Render();
                return false;
            }
            if (_menuInput == null || !isActiveAndEnabled)
                throw new InvalidOperationException("활성 에러 팝업을 먼저 초기화해 주세요.");
            try
            {
                EnsureScreen();
                Render();
                _ownsInput = true;
                _openedFrame = Time.frameCount;
                _setModalInput(true);
                _dismissed = dismissed;
                _screen.Show(_events, _popup.DefaultButton);
                _menuInput.Enable();
                return true;
            }
            catch
            {
                Release();
                throw;
            }
        }

        // 원본은 Unity가 이미 기록했으므로 여기서는 다시 로그를 쓰지 않습니다.
        private void PresentCaptured(ErrorLogMessage message)
        {
            _errorCount += message.Count;
            _details = ErrorPopupDetails.Format(message.Kind, message.Message, message.Stack, _errorCount,
                gameObject.scene.name, _readState?.Invoke() ?? "", message.Time, _logLabel, Application.consoleLogPath);
            Present(null);
        }

        private void BeginCapture()
        {
            if (!_captureUnityErrors) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if ( _collector == null && _menuInput != null && isActiveAndEnabled)
                _collector = new UnityErrorLogCollector(PresentCaptured);
#endif
        }

        protected virtual void OnEnable() => BeginCapture();

#if UNITY_EDITOR
        [ContextMenu("테스트/자동 오류 수집 확인")]
        protected virtual void EmitCaptureTest()
        {
            if (!Application.isPlaying || !IsCapturing)
            {
                Debug.LogWarning("Play 중 데이터·팝업 초기화와 자동 오류 수집 연결을 먼저 완료해 주세요.", this);
                return;
            }
            Debug.LogError("자동 오류 수집 확인용 오류입니다. 실제 게임 오류가 아닙니다.", this);
        }
#endif


        private void EnsureScreen()
        {
            if (_screen != null) return;
            // 비활성 부모에서 참조를 연결한 뒤 표시합니다. 원본 프리팹을 변경하지 않습니다.
            var holder = new GameObject("ErrorPopupStaging");
            holder.SetActive(false);
            try
            {
                _screen = Instantiate(_prefab, holder.transform);
                _screen.gameObject.SetActive(false);
                _screen.transform.SetParent(transform, false);
                _screen.CloseRequested += Close;
                _screen.CopyRequested += Copy;
            }
            finally { Destroy(holder); }
        }

        private string ResolveScene(UnityEngine.Object context)
        {
            if (context is Component component) return component.gameObject.scene.name;
            if (context is GameObject target) return target.scene.name;
            return gameObject.scene.name;
        }

        private void Render() => _screen.SetText(_heading + "\n\n" + _details, _copyText, _closeText);
        private void Submit() { if (_screen.SelectedButton == 1) Copy(); else Close(); }
        private void Cancel() { if (_popup.CancelButton == 1) Copy(); else if (_popup.CancelButton == 2) Close(); }

        private void Copy()
        {
            if (!_ownsInput || _return != null || Time.frameCount <= _openedFrame) return;
            GUIUtility.systemCopyBuffer = _screen.Content;
            if (_copyFeedback != null) StopCoroutine(_copyFeedback);
            _copyFeedback = StartCoroutine(ShowCopied());
        }

        private IEnumerator ShowCopied()
        {
            _screen.ShowStatus(_copiedText);
            yield return new WaitForSecondsRealtime(1.5f);
            if (_screen != null) _screen.ShowStatus("");
            _copyFeedback = null;
        }

        private void Move(int direction) { if (_screen != null) _screen.Move(direction); }

        private void Close()
        {
            if (!_ownsInput || _return != null || Time.frameCount <= _openedFrame) return;
            _menuInput.Disable();
            _screen.Hide();
            _return = StartCoroutine(CompleteClose());
        }

        private IEnumerator CompleteClose()
        {
            // 닫기 입력이 뒤쪽 메뉴의 결정·공격으로 새지 않도록 다음 프레임에 입력 소유권을 돌려줍니다.
            yield return null;
            Action dismissed = _dismissed;
            Release();
            dismissed?.Invoke();
        }

        private void Release()
        {
            _return = null;
            _dismissed = null;
            if (_copyFeedback != null) StopCoroutine(_copyFeedback);
            _copyFeedback = null;
            if (_screen != null) _screen.ShowStatus("");
            _menuInput?.Disable();
            if (_screen != null) _screen.Hide();
            bool owned = _ownsInput;
            _ownsInput = false;
            if (owned) _setModalInput(false);
        }

        protected virtual void OnDisable()
        {
            _collector?.Dispose();
            _collector = null;
            if (_return != null) StopCoroutine(_return);
            Release();
        }

        protected virtual void OnDestroy()
        {
            if (_screen != null)
            {
                _screen.CloseRequested -= Close;
                _screen.CopyRequested -= Copy;
            }
            _menuInput?.Dispose();
        }
    }
}
