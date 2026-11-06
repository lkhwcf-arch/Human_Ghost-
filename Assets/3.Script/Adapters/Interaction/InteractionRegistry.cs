using System;
using System.Collections.Generic;
using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    // 조립자가 전달한 대상 묶음 하나만 관리합니다. 씬 전체 검색과 프레임 탐색은 하지 않습니다.
    public class InteractionRegistry : MonoBehaviour
    {
        private readonly List<IInteractable> _targets = new List<IInteractable>();
        private bool _prepared;
        public IReadOnlyList<IInteractable> Targets => _targets;
        public event Action Changed;
        public void Prepare()
        {
            if (_prepared) return;
            _prepared = true;
            foreach (var target in GetComponentsInChildren<InteractableBehaviour>(true))
                if (target.GetComponentInParent<InteractionRegistry>(true) == this) target.BindRegistry(this);
        }
        public void Register(IInteractable target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (!isActiveAndEnabled || _targets.Contains(target)) return;
            _targets.Add(target);
            target.Changed += NotifyChanged;
            NotifyChanged();
        }
        public void Unregister(IInteractable target)
        {
            if (!_targets.Remove(target)) return;
            target.Changed -= NotifyChanged;
            NotifyChanged();
        }
        public void NotifyChanged() => Changed?.Invoke();
        protected virtual void OnEnable() => Prepare();
        protected virtual void OnDisable()
        {
            _prepared = false;
            while (_targets.Count > 0) Unregister(_targets[_targets.Count - 1]);
        }
    }
}
