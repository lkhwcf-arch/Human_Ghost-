using System;
using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    // 같은 대상 묶음 안에서 등록·해제·변경 알림을 공유합니다.
    public abstract class InteractableBehaviour : MonoBehaviour, IInteractable
    {
        private InteractionRegistry _registry;
        public abstract bool CanInteract { get; }
        // [정수화 미적용 이유] IInteractable 계약의 Transform 월드 위치입니다. 판정은 정수 좌표로 합니다.
        public Vector2 Position => transform.position;
        public abstract int PromptTextId { get; }
        public event Action Changed;
        public abstract void Interact(InteractionContext context);
        public void NotifyChanged() => Changed?.Invoke();
        public void SetPosition(Vector2 position)
        {
            if (Position.Equals(position)) return;
            transform.position = new Vector3(position.x, position.y, transform.position.z);
            NotifyChanged();
        }
        internal void BindRegistry(InteractionRegistry registry)
        {
            if (_registry != registry)
            {
                if (_registry != null) _registry.Unregister(this);
                _registry = registry;
            }
            if (_registry != null && isActiveAndEnabled) _registry.Register(this);
        }
        protected virtual void OnEnable() => BindRegistry(GetComponentInParent<InteractionRegistry>());
        protected virtual void OnDisable() { if (_registry != null) _registry.Unregister(this); }
        protected virtual void OnDestroy() { if (_registry != null) _registry.Unregister(this); _registry = null; }
        protected virtual void OnTransformParentChanged()
        {
            BindRegistry(GetComponentInParent<InteractionRegistry>());
            NotifyChanged();
        }
    }
}
