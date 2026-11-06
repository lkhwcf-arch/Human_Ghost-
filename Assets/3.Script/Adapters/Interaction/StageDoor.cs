using System;
using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    // Door(K15): 로딩 화면을 거쳐 다른 스테이지로 옮기고 도착 앵커에 세웁니다.
    public class StageDoor : InteractableBehaviour
    {
        private int _targetStage;
        private string _arrivalAnchor = "";
        private Func<bool> _canUse;

        public void Configure(int targetStage, string arrivalAnchor, Func<bool> canUse)
        {
            _targetStage = targetStage; _arrivalAnchor = arrivalAnchor; _canUse = canUse;
            NotifyChanged();
        }

        public override bool CanInteract => _targetStage != 0 && (_canUse == null || _canUse());
        public override int PromptTextId => 30066;
        public override void Interact(InteractionContext context) => context.GoToStage(_targetStage, _arrivalAnchor);
    }
}
