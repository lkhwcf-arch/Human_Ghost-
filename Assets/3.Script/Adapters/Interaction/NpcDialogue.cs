using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    [CreateAssetMenu(menuName = "HumanGhost/NPC Dialogue")]
    public sealed class NpcDialogue : ScriptableObject
    {
        [SerializeField, InspectorName("화자")] private string _speaker = "";
        [SerializeField, InspectorName("대사")] private string[] _lines = new string[0];
        [SerializeField, InspectorName("완료 후 목표")] private string _objective = "";
        public string Speaker => _speaker;
        public string[] Lines => (string[])_lines.Clone();
        public string Objective => _objective;
    }
}
