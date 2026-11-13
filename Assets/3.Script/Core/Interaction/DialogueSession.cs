using System;
using System.Collections.Generic;
using System.Linq;
using HumanGhost.Data.Definitions;

namespace HumanGhost.Gameplay
{
    public readonly struct DialogueLine
    {
        public int SpeakerTextId { get; }
        public int TextId { get; }
        public DialogueLine(int speakerTextId, int textId) { SpeakerTextId = speakerTextId; TextId = textId; }
    }

    // 원본 정의와 완료 이력을 분리하며 이 실행 수명의 기록만 보관합니다.
    public class DialogueSession
    {
        private readonly Dictionary<int, Dialogue> _definitions;
        private readonly Func<int, IReadOnlyList<DialogueLine>> _read;
        private readonly HashSet<int> _completed = new HashSet<int>();
        public Dialogue Current { get; private set; }
        public bool IsRunning => Current != null;
        public bool CanBegin(int id) => !IsRunning && (!_completed.Contains(id) || _definitions[id].RepeatMode != "ONCE");
        public DialogueSession(IEnumerable<Dialogue> definitions, Func<int, IReadOnlyList<DialogueLine>> read)
        { _definitions = definitions.ToDictionary(row => row.ID); _read = read ?? throw new ArgumentNullException(nameof(read)); }
        public bool TryBegin(int id, out IReadOnlyList<DialogueLine> lines)
        {
            lines = null;
            if (!CanBegin(id)) return false;
            Dialogue definition = _definitions[id];
            bool completed = _completed.Contains(id);
            if (completed && definition.RepeatMode == "ONCE") return false;
            IReadOnlyList<DialogueLine> source = _read(id);
            if (source == null || source.Count == 0) throw new InvalidOperationException("대화 줄이 공급되지 않았습니다.");
            lines = completed && definition.RepeatMode == "LAST_LINE"
                ? Array.AsReadOnly(new[] { source[source.Count - 1] }) : Array.AsReadOnly(source.ToArray());
            Current = definition;
            return true;
        }
        public bool End(bool completed)
        {
            if (Current == null) return false;
            int id = Current.ID;
            Current = null;
            return completed && _completed.Add(id);
        }
    }
}
