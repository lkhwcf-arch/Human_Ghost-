using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    // 상태와 실행 요청을 연결합니다. 슬롯·파일·회복 규칙은 주입한 실행 담당이 소유합니다.
    public class ShrineInteract : InteractableBehaviour
    {
        [SerializeField, InspectorName("켜진 상태로 시작")] private bool _active = false;
        private System.Action _save;
        public override bool CanInteract => _active && _save != null;
        public override int PromptTextId => 30063;
        public void SetActive(bool active) { if (_active == active) return; _active = active; NotifyChanged(); }
        public void Connect(System.Action save) { _save = save; NotifyChanged(); }
        public override void Interact(InteractionContext context) { if (CanInteract) _save(); }
        protected override void OnDisable() { _save = null; base.OnDisable(); }
    }
}
