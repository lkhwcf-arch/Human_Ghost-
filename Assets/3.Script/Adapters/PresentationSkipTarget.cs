using System;
using UnityEngine;
using UnityEngine.Events;

namespace HumanGhost.Gameplay.Adapters
{
    // 연출 하나의 수명만 관리합니다. 최종 위치·문 상태 등은 연출 소유자가 적용합니다.
    public class PresentationSkipTarget : MonoBehaviour, ISkipTarget
    {
        [SerializeField, InspectorName("최종 상태 적용")] private UnityEvent _applyFinalState = new UnityEvent();
        private bool _running;
        private bool _executing;
        public event Action Skipped;
        public void Begin() { if (!_executing) _running = true; }
        public void Complete() => _running = false;
        public bool CanSkip(SkipScope scope) => scope == SkipScope.Presentation && isActiveAndEnabled && _running && !_executing;
        public bool TrySkip(SkipScope scope)
        {
            if (!CanSkip(scope)) return false;
            _running = false;
            _executing = true;
            try { _applyFinalState.Invoke(); Skipped?.Invoke(); }
            finally { _executing = false; }
            return true;
        }
        protected virtual void OnDisable() => _running = false;
    }
}
