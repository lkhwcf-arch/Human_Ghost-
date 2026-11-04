using System;
using HumanGhost.Gameplay.Adapters;
using HumanGhost.Gameplay.Presentation;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 맵 양 끝 출구(L01·L08·K14)입니다. 오른쪽은 조건이 맞으면 다음 스테이지, 왼쪽은 이전 스테이지로 갑니다. 막히면 안내만 띄웁니다.
    internal sealed class PreviewStageExit
    {
        private const float _edge = 0.6f;
        private readonly PlayerRuntime _player;
        private readonly Bounds _area;
        private readonly int _nextStage, _previousStage;
        private readonly string _fromName;
        private readonly Func<bool> _canLeave;
        private readonly Action<int, string> _go;
        private readonly TextboxScreen _textbox;
        private readonly string _lockedText;
        private bool _touching;

        public PreviewStageExit(PlayerRuntime player, Bounds area, int nextStage, int previousStage, string fromName,
            Func<bool> canLeave, Action<int, string> go, TextboxScreen textbox, string lockedText)
        {
            _player = player; _area = area; _nextStage = nextStage; _previousStage = previousStage; _fromName = fromName;
            _canLeave = canLeave; _go = go; _textbox = textbox; _lockedText = lockedText;
        }

        public void Step()
        {
            float x = _player.transform.position.x;
            bool right = _nextStage != 0 && x >= _area.max.x - 1f - _edge;
            bool left = _previousStage != 0 && x <= _area.min.x + 1f + _edge;
            bool touching = right || left;
            if (touching && !_touching)
            {
                if (left) _go(_previousStage, "Spawn_From" + _fromName);
                else if (_canLeave()) _go(_nextStage, "Spawn_From" + _fromName);
                else _textbox.ShowObjective(_lockedText);
            }
            _touching = touching;
        }
    }
}
