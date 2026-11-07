#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Threading;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Infrastructure;
using HumanGhost.Data.Storage;
using HumanGhost.Gameplay.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace HumanGhost.Gameplay.Adapters
{
    public class DemoResetHost : MonoBehaviour
    {
        [SerializeField, InspectorName("확인 팝업 화면")] private ConfirmationPopupScreen _prefab = null;
        [SerializeField, InspectorName("팝업 데이터 ID")] private int _popupId = 15008;
        private ErrorPopupSceneBinding _modal;
        private EventSystem _events;
        private InputActionAsset _input;
        private Action _validateReturn;
        private Action _returnToTitle;
        private static bool _pending;
        private InputAction _open;
        private MenuInput _menu;
        private IReadOnlyDataCatalog _data;
        private ConfirmationPopupScreen _screen;
        private GameObject _owned;
        private CancellationTokenSource _lifetime;
        private bool _busy;
        private bool _deletionCompleted;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPending() => _pending = false;
        // 씬 조립자가 준비된 공통 의존성을 전달합니다. 기획자는 화면과 문구만 설정합니다.
        public void Initialize(IReadOnlyDataCatalog data, ErrorPopupSceneBinding modal, EventSystem events,
            InputActionAsset input, Action validateReturn, Action returnToTitle)
        {
            if (_lifetime != null) throw new InvalidOperationException("테스트 초기화가 이미 연결되어 있습니다.");
            if (data == null || modal == null || events == null || input == null || _prefab == null || validateReturn == null || returnToTitle == null)
                throw new InvalidOperationException("테스트 초기화의 데이터·모달·입력·팝업·로딩 참조를 연결해 주세요.");
            _data = data; _modal = modal; _events = events; _input = input;
            _validateReturn = validateReturn; _returnToTitle = returnToTitle;
            if (isActiveAndEnabled) BeginInput();
        }
        protected virtual void OnEnable() { if (_data != null) BeginInput(); }
        private void BeginInput()
        {
            if (_lifetime != null) return;
            _lifetime = new CancellationTokenSource();
            _modal.ErrorVisibilityChanged += OnErrorVisibility;
            _open = new InputAction("DemoReset", InputActionType.Button, "<Keyboard>/f12");
            _open.performed += OnOpen;
            _open.Enable();
        }
        private void OnOpen(InputAction.CallbackContext context)
        {
            if (_busy || _screen != null) return;
            _pending = true;
            TryOpen();
        }
        protected virtual void Update() { if (_pending && _data != null && !LoadingScreen.IsActive) TryOpen(); }
        private void TryOpen()
        {
            if (_data == null || LoadingScreen.IsActive || _modal.IsErrorOpen || _screen != null) return;
            _pending = false;
            try
            {
                Popup popup = _data.GetTable<Popup>().Get(_popupId);
                var text = new LanguageTextSource(_data);
                _owned = new GameObject("DemoResetPopup", typeof(RectTransform));
                _owned.transform.SetParent(transform, false);
                var rect = (RectTransform)_owned.transform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
                _owned.SetActive(false);
                _screen = Instantiate(_prefab, _owned.transform);
                _screen.SetText(text.Read(popup.BodyTextID, LanguageCode.Korean),
                    text.Read(popup.Button1TextID, LanguageCode.Korean), text.Read(popup.Button2TextID, LanguageCode.Korean));
                _screen.SetBusy(false);
                _screen.Accepted += Confirm;
                _screen.CloseRequested += Close;
                _menu = new MenuInput(_input, _screen.Move, _screen.Submit, _screen.RequestClose);
                _modal.SetModalOwner(this, true);
                _owned.SetActive(true);
                _screen.Show(_events, popup.DefaultButton);
                _menu.Enable();
            }
            catch (Exception error) { ReleasePopup(); _modal.Report(error, this, "테스트 초기화 팝업 열기"); }
        }
        private void OnErrorVisibility(bool blocked)
        {
            _menu?.SetBlocked(blocked);
            if (_screen != null) _screen.SetBusy(blocked || _busy);
        }
        private async void Confirm()
        {
            if (_busy || _screen == null || _modal.IsErrorOpen) return;
            _busy = true;
            _screen.SetBusy(true);
            _menu.Disable();
            CancellationToken cancellation = _lifetime.Token;
            try
            {
                _validateReturn();
                if (!_deletionCompleted)
                {
                    GameSaveContext owner = GameSaveContext.Current;
                    if (owner != null) { try { await owner.Session.PendingWrite; } catch { cancellation.ThrowIfCancellationRequested(); } }
                    cancellation.ThrowIfCancellationRequested();
                    var store = owner?.Store ?? new JsonSaveStore(GameSaveContext.DirectoryPath("Saves/Demo"), 1);
                    await store.DeleteLatestAsync(cancellation);
                    _deletionCompleted = true;
                    if (ReferenceEquals(GameSaveContext.Current, owner))
                    {
                        GameSaveContext.Set(null);
                    }
                }
                cancellation.ThrowIfCancellationRequested();
                if (this == null || !isActiveAndEnabled) return;
                _returnToTitle();
                // 이동이 끝나기 전에는 원래 게임 입력을 다시 켜지 않습니다.
                if (_screen != null) _screen.Hide();
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                if (this == null || !isActiveAndEnabled || cancellation.IsCancellationRequested) return;
                _busy = false;
                // 삭제 완료 후에는 이동만 재시도하며 다른 슬롯을 삭제하거나 이전 게임으로 복귀하지 않습니다.
                _screen.SetCancelEnabled(!_deletionCompleted);
                _screen.SetBusy(false);
                _menu.Enable();
                _modal.Report(error, this, "최신 저장 슬롯 초기화");
            }
        }
        private void Close() { if (!_busy && !_deletionCompleted) ReleasePopup(); }
        private void ReleasePopup()
        {
            _menu?.Dispose(); _menu = null;
            if (_screen != null) { _screen.Accepted -= Confirm; _screen.CloseRequested -= Close; _screen.Hide(); }
            _screen = null;
            if (_owned != null) Destroy(_owned);
            _owned = null;
            if (_modal != null) _modal.SetModalOwner(this, false);
        }
        protected virtual void OnDisable() => StopInput();
        public void ReleaseBinding()
        {
            StopInput();
            _data = null; _modal = null; _events = null; _input = null; _validateReturn = null; _returnToTitle = null;
        }
        private void StopInput()
        {
            _lifetime?.Cancel(); _lifetime?.Dispose(); _lifetime = null;
            if (_modal != null) _modal.ErrorVisibilityChanged -= OnErrorVisibility;
            if (_open != null) { _open.performed -= OnOpen; _open.Dispose(); _open = null; }
            ReleasePopup(); _busy = false; _deletionCompleted = false;
        }
#if UNITY_EDITOR
        public void StripFromReleaseBuild() => DestroyImmediate(this);
#endif
    }
}
#endif
