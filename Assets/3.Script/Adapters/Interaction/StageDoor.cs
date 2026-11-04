using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    // Door(K15): 로딩 화면을 거쳐 씬을 옮기고 도착 앵커에 세웁니다.
    public sealed class StageDoor : MonoBehaviour, IInteractable
    {
        [SerializeField, InspectorName("갈 씬")] private string _targetScene = "";
        [SerializeField, InspectorName("도착 앵커 이름")] private string _arrivalAnchor = "";

        public bool CanInteract => Application.CanStreamedLevelBeLoaded(_targetScene);
        public Vector2 Position => transform.position;
        public int PromptTextId => 30066;
        public void Interact(InteractionContext context) => context.GoToScene(_targetScene, _arrivalAnchor);
    }
}
