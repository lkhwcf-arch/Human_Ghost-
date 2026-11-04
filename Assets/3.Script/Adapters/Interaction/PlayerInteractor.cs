using System;
using System.Collections.Generic;
using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    // K15 키 한 번 = 동작 하나: 매달리기 → 해체 → 가장 가까운 대상. 표시는 동작할 대상 하나 위에만 띄웁니다.
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [SerializeField, InspectorName("상호작용 대상 묶음")] private Transform _targetRoot = null;
        [SerializeField, InspectorName("표시 글자 프리팹")] private TextMesh _promptPrefab = null;
        [SerializeField, InspectorName("앞뒤 범위 (타일)"), Min(0f)] private float _rangeX = 1f;
        [SerializeField, InspectorName("위아래 범위 (타일)"), Min(0f)] private float _rangeY = 2f;
        [SerializeField, InspectorName("대상 위 표시 높이 (타일)")] private float _promptHeight = 1.5f;
        private readonly List<IInteractable> _targets = new List<IInteractable>();
        private readonly List<InteractionCandidate> _candidates = new List<InteractionCandidate>();
        private InteractionContext _context;
        private Func<bool> _canGrab, _tryGrab, _canExecute, _tryExecute;
        private TextMesh _prompt;
        private IInteractable _selected;

        public void Connect(InteractionContext context, Func<bool> canGrab, Func<bool> tryGrab, Func<bool> canExecute, Func<bool> tryExecute)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _canGrab = canGrab; _tryGrab = tryGrab; _canExecute = canExecute; _tryExecute = tryExecute;
            _targets.Clear();
            if (_targetRoot != null) _targets.AddRange(_targetRoot.GetComponentsInChildren<IInteractable>(true));
            if (_prompt == null && _promptPrefab != null) { _prompt = Instantiate(_promptPrefab, transform); _prompt.gameObject.SetActive(false); }
        }

        public void Disconnect()
        {
            _context = null; _selected = null;
            if (_prompt != null) _prompt.gameObject.SetActive(false);
        }

        // 대화·로딩 중에는 입력 어댑터가 호출하지 않습니다.
        public void Press()
        {
            if (_context == null) return;
            if (_tryGrab()) return;
            if (_tryExecute()) return;
            if (IsOnGround()) Select();
            _selected?.Interact(_context);
        }

        private bool IsOnGround() => _context.Player.Actor.Locomotion.IsGrounded && _context.Player.Actor.Locomotion.CanMove;

        private void Select()
        {
            _selected = null;
            if (!IsOnGround()) return;
            _candidates.Clear();
            foreach (IInteractable target in _targets)
                _candidates.Add(new InteractionCandidate(target.Position.x, target.Position.y, target.CanInteract));
            Vector3 position = _context.Player.transform.position;
            int index = InteractionSelector.Pick(position.x, position.y, _candidates, _rangeX, _rangeY);
            if (index >= 0) _selected = _targets[index];
        }

        private void LateUpdate()
        {
            if (_context == null || _prompt == null) return;
            bool open = _context.Textbox.IsOpen;
            Select();
            Vector3 player = _context.Player.transform.position;
            if (!open && (_canGrab() || _canExecute()))
                ShowPrompt("E", player + Vector3.up * _promptHeight);
            else if (!open && _selected != null)
                ShowPrompt("E " + _context.Text(_selected.PromptTextId), (Vector3)_selected.Position + Vector3.up * _promptHeight);
            else if (_prompt.gameObject.activeSelf)
                _prompt.gameObject.SetActive(false);
        }

        private void ShowPrompt(string text, Vector3 position)
        {
            if (_prompt.text != text) _prompt.text = text;
            _prompt.transform.position = new Vector3(position.x, position.y, _prompt.transform.position.z);
            if (!_prompt.gameObject.activeSelf) _prompt.gameObject.SetActive(true);
        }
    }
}
