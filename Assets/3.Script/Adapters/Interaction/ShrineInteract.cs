using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    // Shrine(K15): 켜졌을 때만 표시합니다. 지스타 빌드는 HP·약초를 바꾸지 않고 저장도 세이브 포인트 통과 때만 합니다(K05_1).
    public class ShrineInteract : InteractableBehaviour
    {
        [SerializeField, InspectorName("켜진 상태로 시작")] private bool _active = false;
        public override bool CanInteract => _active;
        public override int PromptTextId => 30063;
        public void SetActive(bool active) { if (_active == active) return; _active = active; NotifyChanged(); }
        public override void Interact(InteractionContext context) { }
    }
}
