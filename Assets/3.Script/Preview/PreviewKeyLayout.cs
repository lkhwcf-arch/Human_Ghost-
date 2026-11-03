using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HumanGhost.Gameplay.Preview
{
    // 확인 전용 키 배치 시험입니다. 원본 입력 에셋·입력 어댑터·씬을 수정하지 않고,
    // 어댑터가 복제한 에셋에만 런타임 바인딩 덮어쓰기를 적용하고 비활성화 시 제거합니다.
    public sealed class PreviewKeyLayout : MonoBehaviour
    {
        [Serializable]
        public struct Rule
        {
            [SerializeField, InspectorName("Action 이름")] private string _action;
            [SerializeField, InspectorName("기존 경로")] private string _from;
            [SerializeField, InspectorName("시험 경로")] private string _to;
            public string Action => _action;
            public string From => _from;
            public string To => _to;
            public Rule(string action, string from, string to) { _action = action; _from = from; _to = to; }
        }

        // B안: WASD 이동 유지, 오른손 J 공격·K 점프·L 구르기. 처형 E는 확인 장면 기존 키를 그대로 사용합니다.
        private static readonly Rule[] _layoutB =
        {
            new Rule("Attack", "<Keyboard>/x", "<Keyboard>/j"),
            new Rule("Jump", "<Keyboard>/c", "<Keyboard>/k"),
            new Rule("Roll", "<Keyboard>/space", "<Keyboard>/l"),
        };
        public static IReadOnlyList<Rule> LayoutB => _layoutB;

        [SerializeField, InspectorName("원본 입력 에셋")] private InputActionAsset _source = null;
        [SerializeField, InspectorName("덮어쓰기 규칙")] private Rule[] _rules = (Rule[])_layoutB.Clone();
        private readonly HashSet<InputActionAsset> _applied = new HashSet<InputActionAsset>();
        private bool _listening;

        public void Initialize(InputActionAsset source, IReadOnlyList<Rule> rules)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (rules == null || rules.Count == 0) throw new ArgumentException("덮어쓰기 규칙이 필요합니다.", nameof(rules));
            Clear();
            _source = source;
            _rules = new Rule[rules.Count];
            for (int index = 0; index < rules.Count; index++) _rules[index] = rules[index];
            if (isActiveAndEnabled) Begin();
        }

        private void OnEnable() { if (_source != null) Begin(); }
        private void OnDisable() => Clear();

        private void Begin()
        {
            if (_listening) return;
            foreach (Rule rule in _rules)
                if (_source.FindAction(rule.Action) == null) throw new InvalidOperationException($"원본 입력 에셋에 {rule.Action} Action이 없습니다.");
            _listening = true;
            InputSystem.onActionChange += OnActionChange;
            // 이미 초기화된 어댑터의 복제본에도 적용합니다. 이후 활성화는 이벤트로 받습니다.
            foreach (InputAction action in InputSystem.ListEnabledActions()) TryApply(action.actionMap?.asset);
        }

        private void Clear()
        {
            if (_listening) InputSystem.onActionChange -= OnActionChange;
            _listening = false;
            foreach (InputActionAsset asset in _applied)
                if (asset != null) asset.RemoveAllBindingOverrides();
            if (_applied.Count > 0) Debug.Log($"테스트 키 배치를 해제했습니다: 복제 입력 {_applied.Count}개");
            _applied.Clear();
        }

        private void OnActionChange(object target, InputActionChange change)
        {
            if (change == InputActionChange.ActionEnabled) TryApply((target as InputAction)?.actionMap?.asset);
            else if (change == InputActionChange.ActionMapEnabled) TryApply((target as InputActionMap)?.asset);
        }

        private void TryApply(InputActionAsset asset)
        {
            // 원본 에셋은 건드리지 않습니다. 같은 Action ID를 가진 복제본만 대상으로 합니다.
            if (asset == null || asset == _source || _applied.Contains(asset)) return;
            var targets = new InputAction[_rules.Length];
            for (int index = 0; index < _rules.Length; index++)
            {
                targets[index] = asset.FindAction(_source.FindAction(_rules[index].Action).id.ToString());
                if (targets[index] == null) return;
            }
            _applied.Add(asset);
            int count = 0;
            for (int index = 0; index < _rules.Length; index++)
            {
                InputAction action = targets[index];
                for (int binding = 0; binding < action.bindings.Count; binding++)
                    if (string.Equals(action.bindings[binding].path, _rules[index].From, StringComparison.OrdinalIgnoreCase))
                    {
                        action.ApplyBindingOverride(binding, _rules[index].To);
                        count++;
                    }
            }
            Debug.Log($"테스트 키 배치를 적용했습니다: {asset.name}, 바인딩 {count}개");
        }
    }
}
