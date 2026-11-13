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
        [SerializeField, InspectorName("앞뒤 범위 (타일)"), Min(0f)] private float _rangeX = 1f;
        [SerializeField, InspectorName("위아래 범위 (타일)"), Min(0f)] private float _rangeY = 2f;
        [SerializeField, InspectorName("대상 위 표시 높이 (타일)")] private float _promptHeight = 1.5f;
        private InteractionRegistry _registry;
        private SubscriptionScope _scope;
        private readonly List<InteractionCandidate> _candidates = new List<InteractionCandidate>();
        private InteractionContext _context;
        private Func<bool> _canGrab, _tryGrab, _canExecute, _tryExecute;
        private TextMesh _prompt;
        private bool _promptHasKeyIcon;
        private SpriteRenderer _promptKeyIcon, _investigationArtwork;
        private Renderer _promptTextRenderer;
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
            _promptHasKeyIcon = _promptKeyIcon != null;
            _promptTextRenderer = _prompt != null ? _prompt.GetComponent<Renderer>() : null;
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
            if (_registry != null)
            {
                InteractionRegistry registry = _registry;
                scope.Add(() => registry.Changed -= invalidate); registry.Changed += invalidate;
            }
            RequestRefresh();
        }
        public void RequestRefresh() => _scope?.Invalidate();
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
                if (IsMissing(target)) { _candidates.Add(new InteractionCandidate(0f, 0f, false)); continue; }
                Vector2 targetPosition = target.Position;
                _candidates.Add(new InteractionCandidate(targetPosition.x, targetPosition.y,
                    (!(target is Behaviour behaviour) || behaviour.isActiveAndEnabled) && target.CanInteract));
            }
            Vector3 position = _context.Player.transform.position;
            int index = InteractionSelector.Pick(position.x, position.y, _candidates, _rangeX, _rangeY);
            if (index >= 0) _selected = targets[index];
        }

        private void LateUpdate()
        {
            if (!HasContext || _prompt == null || _scope == null || !_scope.TryConsumeRefresh()) return;
            bool open = _context.Textbox.IsOpen;
            if (open)
            {
                _selected = null;
                if (_prompt.gameObject.activeSelf) _prompt.gameObject.SetActive(false);
                return;
            }
            Select();
            Vector3 player = _context.Player.transform.position;
            if (_canGrab() || _canExecute())
                ShowPrompt("E", player + Vector3.up * _promptHeight);
            else if (_selected != null)
            {
                int id = _selected.PromptTextId;
                if (_shownPromptId != id) { _shownPromptId = id; _shownPrompt = "E " + _context.Text(id); }
                ShowPrompt(_shownPrompt, (Vector3)_selected.Position + Vector3.up * _promptHeight, _selected is LedgerInteract);
            }
            else if (_prompt.gameObject.activeSelf)
                _prompt.gameObject.SetActive(false);
        }

        private static bool IsMissing(IInteractable target) => target == null || target is UnityEngine.Object item && item == null;
        private void OnEnable() => StartBindings();
        private void OnDisable() => StopBindings();
        private void OnDestroy() => Disconnect();

        private void ShowPrompt(string text, Vector3 position, bool investigation = false)
        {
            bool artwork = investigation && _investigationArtwork != null;
            if (_investigationArtwork != null) _investigationArtwork.enabled = artwork;
            if (_promptKeyIcon != null) _promptKeyIcon.enabled = !artwork;
            if (artwork) text = "";
            else if (_promptHasKeyIcon) text = text == "E" ? "" : text.StartsWith("E ", StringComparison.Ordinal) ? text.Substring(2) : text;
            if (_prompt.text != text) _prompt.text = text;
            if (!_prompt.gameObject.activeSelf) _prompt.gameObject.SetActive(true);
            if (_promptHasKeyIcon && !artwork && _promptTextRenderer != null)
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
            var next = new Vector3(position.x, position.y + _promptPrefab.transform.localPosition.y, _prompt.transform.position.z);
            if (_prompt.transform.position != next) _prompt.transform.position = next;
        }
    }
}
