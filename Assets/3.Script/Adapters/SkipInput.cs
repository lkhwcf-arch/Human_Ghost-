using System;
using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    // 독립 화면은 이 입력을 사용하고 기존 조립자는 같은 SkipKeyInput을 재사용합니다.
    public class SkipInput : MonoBehaviour
    {
        [SerializeField, InspectorName("스킵 대상 컴포넌트")] private MonoBehaviour _target = null;
        [SerializeField, InspectorName("스킵 범위")] private SkipScope _scope = SkipScope.Presentation;
        private SkipKeyInput _input;
        private bool _blocked;
        public void SetBlocked(bool blocked) => _blocked = blocked;
        protected virtual void OnEnable()
        {
            if (_target == null || !(_target is ISkipTarget))
                throw new InvalidOperationException("스킵 대상을 구현한 컴포넌트를 연결해 주세요.");
            _input = new SkipKeyInput(CanSkip, Execute);
        }
        private bool CanSkip() => !_blocked && _target != null && _target.isActiveAndEnabled && ((ISkipTarget)_target).CanSkip(_scope);
        private void Execute()
        {
            try { ((ISkipTarget)_target).TrySkip(_scope); }
            catch (Exception error) { Debug.LogException(error, this); }
        }
        protected virtual void OnDisable() { _input?.Dispose(); _input = null; }
    }
}
