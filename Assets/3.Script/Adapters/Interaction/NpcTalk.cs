using System.Collections.Generic;
using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    // Talk(K11·K15): 대사를 보여 주고, 다시 말을 걸면 마지막 줄만 보여 줍니다. 저장하지 않습니다.
    public sealed class NpcTalk : MonoBehaviour, IInteractable
    {
        [SerializeField, InspectorName("말하는 사람")] private string _speaker = "";
        [SerializeField, InspectorName("대사 Language ID")] private int[] _lineIds = new int[0];
        [SerializeField, InspectorName("임시 대사(시나리오 공급 전)")] private string[] _temporaryLines = new string[0];
        [SerializeField, InspectorName("대화 뒤 임시 목표 안내")] private string _objective = "";
        private bool _talked;

        public bool CanInteract => true;
        public Vector2 Position => transform.position;
        public int PromptTextId => 30064;

        public void Interact(InteractionContext context)
        {
            var lines = new List<string>();
            foreach (int id in _lineIds) lines.Add(context.Text(id));
            if (lines.Count == 0) lines.AddRange(_temporaryLines);
            if (lines.Count == 0) throw new System.InvalidOperationException(name + "의 대사를 연결해 주세요.");
            if (_talked) lines = new List<string> { lines[lines.Count - 1] };
            bool first = !_talked;
            _talked = true;
            context.Textbox.Open(_speaker, lines, () => { if (first && _objective.Length > 0) context.Textbox.ShowObjective(_objective); });
        }
    }
}
