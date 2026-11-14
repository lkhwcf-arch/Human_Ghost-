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
        private int _promptTextId;
        private Action _travel;

        // travel을 주면 조립자가 이동과 이동 후속 처리(경로 통계 등)를 함께 소유합니다.
        public void Configure(int targetStage, string arrivalAnchor, Func<bool> canUse, int promptTextId = 0, Action travel = null)
        {
            _targetStage = targetStage; _arrivalAnchor = arrivalAnchor; _canUse = canUse; _travel = travel;
            _promptTextId = promptTextId;
            NotifyChanged();
        }

        public override bool CanInteract => _targetStage != 0 && (_canUse == null || _canUse());
        public override int PromptTextId => _promptTextId;
        public override void Interact(InteractionContext context)
        {
            if (_travel != null) _travel();
            else context.GoToStage(_targetStage, _arrivalAnchor);
        }
    }
}
