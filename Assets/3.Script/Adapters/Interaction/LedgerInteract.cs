using HumanGhost.Data.Definitions;
using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    // Ledger(K15): 처음 조사하면 맵을 클리어하고 결박 가르기를 해금합니다. 명부 컷씬은 공급 뒤 연결합니다.
    public class LedgerInteract : InteractableBehaviour
    {
        [SerializeField, InspectorName("클리어할 Stage ID")] private int _stageId = 8005;
        [SerializeField, InspectorName("해금 Skill ID")] private int _skillId = 7001;
        [SerializeField, InspectorName("해금 문구 Language ID")] private int _unlockTextId = 30023;
        [SerializeField, InspectorName("임시 재조사 문구(시나리오 공급 전)")] private string _temporaryRepeatLine = "";

        public void Configure(string temporaryRepeatLine) { _temporaryRepeatLine = temporaryRepeatLine; NotifyChanged(); }

        public override bool CanInteract => true;
        public override int PromptTextId => 30065;

        public override void Interact(InteractionContext context)
        {
            if (context.Progress.Clear(_stageId))
            {
                context.Player.EnableSkill(context.Data.GetTable<Skill>().Get(_skillId));
                context.Textbox.ShowNotice(context.Text(_unlockTextId));
                return;
            }
            context.Textbox.Open("", new[] { _temporaryRepeatLine }, null);
        }
    }
}
