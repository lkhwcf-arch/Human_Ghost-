using System;
using System.Collections.Generic;

namespace HumanGhost.Gameplay
{
    // 텍스트박스 진행(O10)입니다. 글자가 나오는 중 넘기면 그 줄을 다 보여 주고, 다 나온 뒤 넘기면 다음 줄, 마지막 줄 뒤에는 끝납니다.
    public sealed class TextboxPlayback
    {
        private readonly IReadOnlyList<string> _lines;
        private readonly int _millisecondsPerCharacter;
        private int _elapsed;
        private int _line;

        public bool IsFinished => _line >= _lines.Count;
        public string CurrentLine => IsFinished ? "" : _lines[_line];
        public int VisibleCharacters { get; private set; }
        public bool IsLineComplete => !IsFinished && VisibleCharacters >= CurrentLine.Length;

        public TextboxPlayback(IReadOnlyList<string> lines, int millisecondsPerCharacter)
        {
            if (lines == null || lines.Count == 0) throw new ArgumentException("표시할 줄이 필요합니다.", nameof(lines));
            if (millisecondsPerCharacter <= 0) throw new ArgumentOutOfRangeException(nameof(millisecondsPerCharacter));
            _lines = lines; _millisecondsPerCharacter = millisecondsPerCharacter;
        }

        public void Tick(int milliseconds)
        {
            if (milliseconds < 0) throw new ArgumentOutOfRangeException(nameof(milliseconds));
            if (IsFinished || IsLineComplete) return;
            _elapsed += milliseconds;
            VisibleCharacters = Math.Min(CurrentLine.Length, _elapsed / _millisecondsPerCharacter);
        }

        public void Advance()
        {
            if (IsFinished) return;
            if (!IsLineComplete) { VisibleCharacters = CurrentLine.Length; return; }
            _line++; _elapsed = 0; VisibleCharacters = 0;
        }
    }
}
