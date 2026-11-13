using System.Collections.Generic;
using UnityEngine;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Storage;

namespace HumanGhost.Gameplay.Adapters
{
    // Talk(K11·K15): 대사를 보여 주고, 다시 말을 걸면 마지막 줄만 보여 줍니다. 저장하지 않습니다.
    public class NpcTalk : InteractableBehaviour
    {
        [SerializeField, InspectorName("말하는 사람")] private string _speaker = "";
        [SerializeField, InspectorName("대사 Language ID")] private int[] _lineIds = new int[0];
        [SerializeField, InspectorName("임시 대사(시나리오 공급 전)")] private string[] _temporaryLines = new string[0];
        [SerializeField, InspectorName("대화 뒤 임시 목표 안내")] private string _objective = "";
        private bool _talked;
        [SerializeField, InspectorName("Npc 테이블 ID (0이면 기존 임시 연결)")] private int _npcId;
        private Npc _definition;
        private DialogueRunner _dialogues;
        private TextMesh _nameLabel;
        private SubscriptionScope _dialogueScope;
        public int NpcId => _npcId;
        public void SetDefinitionId(int id) => _npcId = id;
        public void Configure(int npcId, IReadOnlyDataCatalog data, DialogueRunner dialogues, TextMesh nameTemplate = null)
        {
            _dialogueScope?.Dispose(); _dialogueScope = null;
            _dialogues?.Cancel(this);
            _npcId = npcId; _definition = data.GetTable<Npc>().Get(npcId);
            _dialogues = dialogues ?? throw new System.ArgumentNullException(nameof(dialogues));
            if (nameTemplate != null && _nameLabel == null)
            {
                _nameLabel = Instantiate(nameTemplate, transform);
                _nameLabel.name = "NpcName";
                _nameLabel.transform.localPosition = Vector3.up * 2.5f;
                _nameLabel.text = _dialogues.ReadText(_definition.NameTextID);
                _nameLabel.gameObject.SetActive(_definition.ContentEnabled);
            }
            NotifyChanged();
            ConnectDialogueEvents();
        }
        private void ConnectDialogueEvents()
        {
            if (!isActiveAndEnabled || _dialogues == null || _dialogueScope != null) return;
            _dialogueScope = new SubscriptionScope();
            DialogueRunner runner = _dialogues;
            System.Action changed = _dialogueScope.Guard(() =>
            {
                try
                {
                    if (this == null || !isActiveAndEnabled) return;
                    if (_nameLabel != null && _definition != null) _nameLabel.text = runner.ReadText(_definition.NameTextID);
                    NotifyChanged();
                }
                catch (System.Exception error) { Debug.LogException(error, this); }
            });
            _dialogueScope.Add(() => LanguageSettings.Changed -= changed); LanguageSettings.Changed += changed;
            _dialogueScope.Add(() => runner.AvailabilityChanged -= changed); runner.AvailabilityChanged += changed;
        }

        // 맵 표식에 조립자가 붙일 때 씁니다.
        public void Configure(string speaker, string[] temporaryLines, string objective)
        {
            _speaker = speaker; _temporaryLines = temporaryLines; _objective = objective;
            NotifyChanged();
        }

        public override bool CanInteract => _definition == null || _definition.ContentEnabled && _definition.Type == "NPC"
            && _definition.DialogueID != 0 && _dialogues != null && _dialogues.CanPlay(_definition.DialogueID);
        public override int PromptTextId => 30064;

        public override void Interact(InteractionContext context)
        {
            if (_definition != null)
            {
                if (CanInteract && _definition.Type == "NPC") _dialogues.TryPlay(_definition.DialogueID, this);
                return;
            }
            var lines = new List<string>();
            foreach (int id in _lineIds) lines.Add(context.Text(id));
            if (lines.Count == 0) lines.AddRange(_temporaryLines);
            if (lines.Count == 0) throw new System.InvalidOperationException(name + "의 대사를 연결해 주세요.");
            if (_talked) lines = new List<string> { lines[lines.Count - 1] };
            bool first = !_talked;
            _talked = true;
            context.Textbox.Open(_speaker, lines, () => { if (first && _objective.Length > 0) context.Textbox.ShowObjective(_objective); });
        }
        protected override void OnEnable() { base.OnEnable(); ConnectDialogueEvents(); }
        protected override void OnDisable()
        { _dialogueScope?.Dispose(); _dialogueScope = null; _dialogues?.Cancel(this); base.OnDisable(); }
    }
}
