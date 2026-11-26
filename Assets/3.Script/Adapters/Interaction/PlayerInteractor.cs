using System;
using System.Collections.Generic;
using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    // K15 키 한 번 = 동작 하나: 매달리기 → 해체 → 가장 가까운 대상. 표시는 동작할 대상 하나 위에만 띄웁니다.
    [DefaultExecutionOrder(1100)]
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField, InspectorName("상호작용 대상 묶음")] private Transform _targetRoot = null;
        [SerializeField, InspectorName("표시 글자 프리팹")] private TextMesh _promptPrefab = null;
        [SerializeField, InspectorName("앞뒤 범위 (tile_x100)"), Min(0)] private int _rangeXUnits = 100;
        [SerializeField, InspectorName("위아래 범위 (tile_x100)"), Min(0)] private int _rangeYUnits = 200;
        // [정수화 미적용 이유] 상호작용 안내를 대상 위에 띄우는 Transform 표시 높이(타일 기준 월드 거리)입니다. 선택 판정에 쓰지 않습니다.
        [SerializeField, InspectorName("대상 위 표시 높이 (타일)")] private float _promptHeight = 1.5f;
        private InteractionRegistry _registry;
        private SubscriptionScope _scope;
        private readonly List<InteractionCandidate> _candidates = new List<InteractionCandidate>();
        private InteractionContext _context;
        private Func<bool> _canGrab, _tryGrab, _canExecute, _tryExecute;
        private TextMesh _prompt;
        [SerializeField, InspectorName("상호작용 F 키 그림 (없으면 글자 표시)")] private Sprite _interactionKeySprite = null;
        private bool _promptHasKeyIcon;
        private bool _promptHidden;
        private SpriteRenderer _promptKeyIcon, _investigationArtwork;
        private Renderer _promptTextRenderer;
        private SpriteRenderer _promptBackdrop, _promptBorder;
        private IInteractable _selected;
        private int? _shownPromptId;
        private string _shownPrompt;
        private bool HasContext => _context != null && _context.Player != null && _context.Player.Actor != null && _context.Textbox != null;

        public void Connect(InteractionContext context, Func<bool> canGrab, Func<bool> tryGrab, Func<bool> canExecute, Func<bool> tryExecute)
        {
            StopBindings();
            if (canGrab == null || tryGrab == null || canExecute == null || tryExecute == null)
                throw new ArgumentNullException(nameof(canGrab), "상호작용 관측과 실행 함수를 모두 연결해 주세요.");
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _shownPromptId = null; _shownPrompt = null;
            _canGrab = canGrab; _tryGrab = tryGrab; _canExecute = canExecute; _tryExecute = tryExecute;
            PrepareRegistry();
            if (_prompt == null && _promptPrefab != null) { _prompt = Instantiate(_promptPrefab, transform); _prompt.gameObject.SetActive(false); }
            _promptKeyIcon = _prompt != null ? _prompt.transform.Find("KeyIcon")?.GetComponent<SpriteRenderer>() : null;
            _investigationArtwork = _prompt != null ? _prompt.transform.Find("InvestigationArtwork")?.GetComponent<SpriteRenderer>() : null;
            _promptHasKeyIcon = _promptKeyIcon != null && _interactionKeySprite != null;
            if (_promptKeyIcon != null) { _promptKeyIcon.sprite = _promptHasKeyIcon ? _interactionKeySprite : null; _promptKeyIcon.enabled = _promptHasKeyIcon; }
            _promptTextRenderer = _prompt != null ? _prompt.GetComponent<Renderer>() : null;
            _promptBackdrop = _prompt != null ? _prompt.transform.Find("Backdrop")?.GetComponent<SpriteRenderer>() : null;
            _promptBorder = _prompt != null ? _prompt.transform.Find("Border")?.GetComponent<SpriteRenderer>() : null;
            StartBindings();
        }

        // 맵을 런타임에 조립한 뒤 대상 묶음을 전달합니다.
        public void SetTargetRoot(Transform root)
        {
            StopBindings();
            _targetRoot = root;
            PrepareRegistry();
            StartBindings();
        }
        private void PrepareRegistry()
        {
            _registry = _targetRoot == null ? null : _targetRoot.GetComponent<InteractionRegistry>();
            if (_targetRoot != null && _registry == null) _registry = _targetRoot.gameObject.AddComponent<InteractionRegistry>();
            if (_registry != null) _registry.Prepare();
        }
        private void StartBindings()
        {
            if (_scope != null || !isActiveAndEnabled || !HasContext) return;
            _scope = new SubscriptionScope();
            SubscriptionScope scope = _scope;
            Action invalidate = scope.Invalidate;
            Action<bool> textboxChanged = value => scope.Invalidate();
            Action<HumanGhost.Data.Definitions.StateKey> stateChanged = value => scope.Invalidate();
            PlayerRuntime player = _context.Player;
            PlayerActor actor = player.Actor;
            var textbox = _context.Textbox;
            StageProgress progress = _context.Progress;
            scope.Add(() => player.SpatialChanged -= invalidate); player.SpatialChanged += invalidate;
            Action disconnect = scope.Guard(Disconnect);
            scope.Add(() => player.Destroyed -= disconnect); player.Destroyed += disconnect;
            scope.Add(() => actor.States.Changed -= stateChanged); actor.States.Changed += stateChanged;
            scope.Add(() => textbox.OpenChanged -= textboxChanged); textbox.OpenChanged += textboxChanged;
            scope.Add(() => progress.Changed -= invalidate); progress.Changed += invalidate;
            Action languageChanged = () => { _shownPromptId = null; scope.Invalidate(); };
            scope.Add(() => LanguageSettings.Changed -= languageChanged); LanguageSettings.Changed += languageChanged;
            scope.Add(() => KeyboardControls.Changed -= invalidate); KeyboardControls.Changed += invalidate;
            if (_registry != null)
            {
                InteractionRegistry registry = _registry;
                scope.Add(() => registry.Changed -= invalidate); registry.Changed += invalidate;
            }
            RequestRefresh();
        }
        public bool IsShrinePromptVisible => HasContext && _prompt != null && _prompt.gameObject.activeSelf && _selected is ShrineInteract;
        public void RequestRefresh() => _scope?.Invalidate();
        // 서낭당처럼 화면이 월드를 덮는 동안 월드 안내를 숨깁니다. 대화창과 같은 숨김 경로를 씁니다.
        public void SetPromptHidden(bool hidden) { if (_promptHidden == hidden) return; _promptHidden = hidden; RequestRefresh(); }
        private void StopBindings()
        {
            _scope?.Dispose(); _scope = null;
            _selected = null; _candidates.Clear();
            if (_prompt != null) _prompt.gameObject.SetActive(false);
        }

        public void Disconnect()
        {
            StopBindings();
            _context = null; _selected = null;
            _registry = null;
            _shownPromptId = null; _shownPrompt = null;
            _canGrab = null; _tryGrab = null; _canExecute = null; _tryExecute = null;
            if (_prompt != null) _prompt.gameObject.SetActive(false);
        }

        // 대화·로딩 중에는 입력 어댑터가 호출하지 않습니다.
        public void Press()
        {
            if (this == null || !isActiveAndEnabled || !HasContext || _context.Textbox.IsOpen) return;
            if (_tryGrab()) return;
            if (_tryExecute()) return;
            Select();
            _selected?.Interact(_context);
        }

        private bool IsOnGround() => HasContext && _context.Player.Actor.Locomotion.IsGrounded && _context.Player.Actor.Locomotion.CanMove;

        private void Select()
        {
            _selected = null;
            if (!IsOnGround() || _registry == null) return;
            _candidates.Clear();
            IReadOnlyList<IInteractable> targets = _registry.Targets;
            for (int i = 0; i < targets.Count; i++)
            {
                IInteractable target = targets[i];
                if (IsMissing(target)) { _candidates.Add(new InteractionCandidate(0, 0, false)); continue; }
                Vector2 targetPosition = target.Position;
                IntegerPosition observed = _context.Player.FromWorld(targetPosition);
                _candidates.Add(new InteractionCandidate(observed.X, observed.Y,
                    (!(target is Behaviour behaviour) || behaviour.isActiveAndEnabled) && target.CanInteract));
            }
            IntegerPosition position = _context.Player.Position;
            int index = InteractionSelector.Pick(position.X, position.Y, _candidates,
                IntegerPosition.FromData(_rangeXUnits), IntegerPosition.FromData(_rangeYUnits));
            if (index >= 0) _selected = targets[index];
        }

        private void LateUpdate()
        {
            if (!HasContext || _prompt == null || _scope == null || !_scope.TryConsumeRefresh()) return;
            bool open = _context.Textbox.IsOpen || _promptHidden;
            if (open)
            {
                _selected = null;
                if (_prompt.gameObject.activeSelf) _prompt.gameObject.SetActive(false);
                return;
            }
            Select();
            Vector3 player = _context.Player.transform.position;
            // 상호작용 키 이름은 공통 키 배치 조회에서 읽습니다. 글자 키라 언어 문구로 바꾸지 않습니다.
            string interactKey = KeyboardControls.ReadActionKey("Interact");
            if (_canGrab() || _canExecute())
                ShowPrompt(interactKey, "", player + Vector3.up * _promptHeight);
            else if (_selected != null)
            {
                int id = _selected.PromptTextId;
                // 대상이 고른 Language 문구를 그대로 씁니다. 기존 UI_Lang 51901~51905 매핑은 같은 TextID를 돌아 읽던 중복이라 제거했습니다(S03).
                if (_shownPromptId != id) { _shownPromptId = id; _shownPrompt = _context.Text(id); }
                // 안내 위치를 알려 주는 대상(NPC 이름표 위)은 안내 묶음의 아랫변을 그 위치에 맞추고, 나머지(서낭당·문·명부)는 기존 높이를 씁니다.
                if (_selected is IInteractionPromptAnchor anchor && anchor.TryGetPromptBase(out Vector2 promptBase))
                    ShowPrompt(interactKey, _shownPrompt, promptBase, _selected is LedgerInteract, alignBottom: true);
                else ShowPrompt(interactKey, _shownPrompt, (Vector3)_selected.Position + Vector3.up * _promptHeight, _selected is LedgerInteract);
            }
            else if (_prompt.gameObject.activeSelf)
                _prompt.gameObject.SetActive(false);
        }

        private static bool IsMissing(IInteractable target) => target == null || target is UnityEngine.Object item && item == null;
        private void OnEnable() => StartBindings();
        private void OnDisable() => StopBindings();
        private void OnDestroy() => Disconnect();

        // key는 키 이름, label은 동작 문구(없으면 빈 문자열)입니다. 키 그림이 있으면 글자에는 동작 문구만 남깁니다.
        private void ShowPrompt(string key, string label, Vector3 position, bool investigation = false, bool alignBottom = false)
        {
            // 기존 조사 그림에는 E 키가 포함돼 있습니다. F 그림 공급 전에는 공통 글자 안내를 씁니다.
            bool artwork = false;
            if (_investigationArtwork != null) _investigationArtwork.enabled = artwork;
            bool showIcon = !artwork && _promptHasKeyIcon && key == "F";
            if (_promptKeyIcon != null) _promptKeyIcon.enabled = showIcon;
            string text = artwork ? "" : showIcon ? label : label.Length == 0 ? key : key + " " + label;
            if (_prompt.text != text) _prompt.text = text;
            if (!_prompt.gameObject.activeSelf) _prompt.gameObject.SetActive(true);
            if (showIcon && _promptTextRenderer != null)
            {
                float keyWidth = _promptKeyIcon.bounds.size.x;
                float textWidth = text.Length > 0 ? _promptTextRenderer.bounds.size.x : 0f;
                float gap = keyWidth * 0.2f;
                Vector3 keyPosition = _promptKeyIcon.transform.localPosition;
                keyPosition.x = textWidth > 0 ? _prompt.transform.InverseTransformVector(
                    Vector3.left * ((textWidth + keyWidth) * 0.5f + gap)).x : 0f;
                _promptKeyIcon.transform.localPosition = keyPosition;
                if (textWidth > 0) position.x += (keyWidth + gap) * 0.5f;
            }
            // 아랫변 맞춤에서는 프리팹 높이 보정을 더하지 않습니다(최종 위치 계산을 한곳에서 하여 보정이 두 번 더해지지 않게).
            var next = new Vector3(position.x, position.y + (alignBottom ? 0f : _promptPrefab.transform.localPosition.y), _prompt.transform.position.z);
            if (_prompt.transform.position != next) _prompt.transform.position = next;
            UpdatePromptBackdrop();
            if (alignBottom) AlignPromptBottom(position.y);
        }

        // 글자·키 아이콘·배경·테두리 배치가 끝난 뒤의 실제 아랫변을 기준 높이에 맞춥니다.
        // 이동량은 정수로 계산하고 Transform 대입 때만 월드 좌표로 바꿉니다(표시 경계).
        private void AlignPromptBottom(float baseY)
        {
            Renderer lowest = _promptBorder != null && _promptBorder.enabled ? _promptBorder
                : _promptBackdrop != null && _promptBackdrop.enabled ? _promptBackdrop : _promptTextRenderer;
            if (lowest == null) return;
            PlayerRuntime player = _context.Player;
            long shift = checked(player.ToInteger(baseY) - player.ToInteger(lowest.bounds.min.y));
            if (shift == 0) return;
            _prompt.transform.position += Vector3.up * player.ToWorld(shift);
        }
        private void UpdatePromptBackdrop()
        {
            if (_promptBackdrop == null || _promptTextRenderer == null) return;
            Bounds bounds = _promptTextRenderer.bounds;
            if (_promptHasKeyIcon && _promptKeyIcon != null && _promptKeyIcon.enabled) bounds.Encapsulate(_promptKeyIcon.bounds);
            Vector3 size = _prompt.transform.InverseTransformVector(bounds.size + new Vector3(.24f, .14f, 0f));
            size = new Vector3(Mathf.Max(.3f, size.x), Mathf.Max(.25f, size.y), 1f);
            Vector3 center = _prompt.transform.InverseTransformPoint(bounds.center); center.z = .03f;
            _promptBackdrop.transform.localPosition = center; _promptBackdrop.transform.localScale = size;
            if (_promptBorder == null) return;
            _promptBorder.transform.localPosition = center + Vector3.forward * .01f;
            _promptBorder.transform.localScale = size + new Vector3(.04f, .04f, 0f);
        }
    }
}
