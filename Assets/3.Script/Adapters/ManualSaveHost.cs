#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Storage;
using HumanGhost.Gameplay.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HumanGhost.Gameplay.Adapters
{
    // 독립 프리팹의 연결 창구입니다. 게임 상태·저장 폴더를 새로 만들지 않고 기존 흐름을 주입받습니다.
    public sealed class ManualSaveHost : MonoBehaviour, IModalInputTarget
    {
        [SerializeField] private SaveSlotScreen _screen = null;
        [SerializeField] private InputActionAsset _input = null;
        [SerializeField] private GameObject _pointerEvents = null;
        [SerializeField, InspectorName("저장 확인 팝업 ID")] private int _confirmationPopupId = 15001;
        [SerializeField, InspectorName("덮어쓰기 팝업 ID")] private int _overwritePopupId = 15002;
        [SerializeField, InspectorName("목록 제목 문구 ID")] private int _titleTextId = 30207;
        [SerializeField, InspectorName("빈 칸 문구 ID")] private int _emptyTextId = 30208;
        [SerializeField, InspectorName("저장 성공 문구 ID")] private int _successTextId = 30024;
        private SaveGameFlow _flow;
        private Func<bool> _canOpen;
        private MenuInput _menuInput;
        private InputAction _open;
        private Popup _savePopup, _overwritePopup;
        private IReadOnlyDataTable<Language> _language;
        private LanguageTextSource _text;
        private int _generation;
        private bool _saving;
        private bool _modalBlocked;
        public void SetModalBlocked(bool blocked)
        {
            _modalBlocked = blocked;
            _menuInput?.SetBlocked(blocked);
            if (_screen != null && IsOpen) _screen.SetBusy(_saving || blocked);
        }
        public bool IsOpen { get; private set; }

        public void Connect(SaveGameFlow flow, IReadOnlyDataCatalog data, Func<bool> canOpen)
        {
            if (_flow != null) throw new InvalidOperationException("수동 저장 실행 담당이 이미 연결되어 있습니다.");
            if (flow == null || data == null || canOpen == null) throw new ArgumentNullException(nameof(flow));
            var language = data.GetTable<Language>();
            Popup savePopup = ReadPopup(data, language, _confirmationPopupId, false);
            Popup overwritePopup = ReadPopup(data, language, _overwritePopupId, true);
            foreach (int id in new[] { _titleTextId, _emptyTextId, _successTextId }) language.Get(id);
            _menuInput = new MenuInput(_input, _screen.Move, _screen.Submit, _screen.Cancel);
            _open = new InputAction("ManualSave", InputActionType.Button, "<Keyboard>/f5");
            _open.performed += OnOpen;
            _savePopup = savePopup;
            _overwritePopup = overwritePopup;
            _language = language;
            _text = new LanguageTextSource(data);
            _canOpen = canOpen;
            _flow = flow;
            if (isActiveAndEnabled && flow.ManualSaveEnabled) _open.Enable();
        }

        private static Popup ReadPopup(IReadOnlyDataCatalog data, IReadOnlyDataTable<Language> language, int id, bool overwrite)
        {
            Popup popup = data.GetTable<Popup>().Get(id);
            if (popup.Button3TextID != 0 || popup.DefaultButton < 1 || popup.DefaultButton > 2 || popup.CancelButton != 2
                || (overwrite && popup.DefaultButton != 2))
                throw new InvalidOperationException("수동 저장은 예·아니오 두 버튼과 취소=아니오, 덮어쓰기 기본=아니오가 필요합니다.");
            foreach (int textId in new[] { popup.BodyTextID, popup.Button1TextID, popup.Button2TextID }) language.Get(textId);
            return popup;
        }

        public void Disconnect(SaveGameFlow owner)
        {
            if (_flow != owner) return;
            ++_generation;
            _menuInput?.Dispose(); _menuInput = null;
            if (_open != null) { _open.performed -= OnOpen; _open.Dispose(); _open = null; }
            _flow?.CloseManualSave(false);
            _flow = null;
            _canOpen = null;
            _language = null;
            IsOpen = false;
            _saving = false;
            if (_screen != null) _screen.Hide();
            if (_pointerEvents != null) _pointerEvents.SetActive(false);
        }

        private void Awake() { _screen.Hide(); _pointerEvents.SetActive(false); }
        private void OnEnable()
        {
            _screen.SelectionRequested += Select;
            _screen.CloseRequested += Close;
            if (_flow != null && _flow.ManualSaveEnabled) _open.Enable();
        }
        private void OnDisable()
        {
            _screen.SelectionRequested -= Select;
            _screen.CloseRequested -= Close;
            Disconnect(_flow);
        }
        private void OnOpen(InputAction.CallbackContext context) => Open();

        public void Open()
        {
            if (_modalBlocked || !isActiveAndEnabled || IsOpen || _flow == null || !_canOpen()) return;
            try
            {
                if (!_flow.TryOpenManualSave()) return;
                IsOpen = true;
                ++_generation;
                ShowSlots("");
                _pointerEvents.SetActive(true);
                _menuInput.Enable();
            }
            catch (Exception error)
            {
                Close();
                Debug.LogError($"수동 저장 화면을 열지 못했습니다.\n{error.Message}", this);
            }
        }

        private void ShowSlots(string status) => _screen.Show((_titleTextId == 30207 ? _text.ReadUi(51801) : _text.Read(_titleTextId)),
            _flow.GetSlots(true), (_emptyTextId == 30208 ? _text.ReadUi(51802) : _text.Read(_emptyTextId)), status);

        private void Select(int index)
        {
            if (_modalBlocked || !IsOpen || _saving) return;
            SaveSlotEntry entry = _flow.GetSlots(true)[index];
            bool overwrite = entry.Record != null;
            Popup popup = overwrite ? _overwritePopup : _savePopup;
            string question = LanguageTextSource.Select(_language.Get(popup.BodyTextID));
            if (overwrite) question += "\n" + SaveSlotScreen.FormatDate(entry.Record);
            _screen.Confirm(question, LanguageTextSource.Select(_language.Get(popup.Button1TextID)), LanguageTextSource.Select(_language.Get(popup.Button2TextID)),
                popup.DefaultButton == 1, false, accepted => { if (accepted) Save(entry.Slot, overwrite); });
        }

        private async void Save(int slot, bool overwriteConfirmed)
        {
            if (_modalBlocked || !IsOpen || _saving) return;
            SaveGameFlow flow = _flow;
            int generation = _generation;
            _saving = true;
            _screen.SetBusy(true);
            try
            {
                bool saved = await flow.SaveManualAsync(slot, overwriteConfirmed);
                if (!IsCurrent(generation)) return;
                ShowSlots(saved ? (_successTextId == 30024 ? _text.ReadUi(51803) : _text.Read(_successTextId)) : _text.ReadUi(51804));
            }
            catch (Exception error)
            {
                if (!IsCurrent(generation)) return;
                _screen.ShowStatus("저장 요청 처리 중 오류가 발생했습니다. 콘솔을 확인해 주세요.");
                Debug.LogError($"수동 저장 요청 처리에 실패했습니다.\n{error.Message}", this);
            }
            finally { if (IsCurrent(generation)) { _saving = false; _screen.SetBusy(_modalBlocked); } }
        }

        public void Close()
        {
            if (_modalBlocked || !IsOpen || _saving || !_flow.CloseManualSave()) return;
            ++_generation;
            IsOpen = false;
            _menuInput.Disable();
            _screen.Hide();
            _pointerEvents.SetActive(false);
        }
        private bool IsCurrent(int generation) => this != null && isActiveAndEnabled && IsOpen && generation == _generation;
#if UNITY_EDITOR
        public void StripFromReleaseBuild() => DestroyImmediate(gameObject);
#endif
    }
}
#endif
