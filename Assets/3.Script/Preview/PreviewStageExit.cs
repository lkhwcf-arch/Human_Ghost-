using System;
using HumanGhost.Gameplay.Adapters;
using HumanGhost.Gameplay.Presentation;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 맵 오른쪽 출구(L01·L03·K14)입니다. 명부 조사 전에는 안내만 띄웁니다. 다음 맵이 연결되면 이동을 넣습니다.
    internal sealed class PreviewStageExit
    {
        private readonly PlayerRuntime _player;
        private readonly Transform _exit;
        private readonly Func<bool> _canLeave;
        private readonly TextboxScreen _textbox;
        private readonly string _lockedText;
        private readonly string _openText;
        private bool _touching;

        public PreviewStageExit(PlayerRuntime player, Transform exit, Func<bool> canLeave, TextboxScreen textbox, string lockedText, string openText)
        {
            _player = player; _exit = exit; _canLeave = canLeave; _textbox = textbox; _lockedText = lockedText; _openText = openText;
        }

        public void Step()
        {
            bool touching = _player.transform.position.x >= _exit.position.x;
            if (touching && !_touching) _textbox.ShowObjective(_canLeave() ? _openText : _lockedText);
            _touching = touching;
        }
    }
}
