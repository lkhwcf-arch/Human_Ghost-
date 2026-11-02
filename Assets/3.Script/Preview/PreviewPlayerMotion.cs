using System;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay.Adapters;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 확인 장면 전용 이동입니다. 코어의 점프·구르기 요청을 임시 Transform 표시로 바꿉니다.
    internal sealed class PreviewPlayerMotion
    {
        private readonly PlayerRuntime _player;
        private readonly float _groundY;
        private float _jumpElapsed;
        private float _rollRemaining;
        private float _rollSpeed;
        private bool _jumping;

        public PreviewPlayerMotion(PlayerRuntime player, float groundY)
        {
            _player = player != null ? player : throw new ArgumentNullException(nameof(player));
            _groundY = groundY;
        }

        public void StartJump()
        {
            _jumpElapsed = 0f;
            _jumping = true;
        }

        public void StartRoll(int direction, int distance, int duration, float worldUnitsPerTile)
        {
            _rollRemaining = duration / 1000f;
            _rollSpeed = duration > 0 ? direction * distance / 100f * worldUnitsPerTile / _rollRemaining : 0f;
        }

        public void Move(float seconds, float worldUnitsPerTile, float jumpSeconds, float stageHalfWidth)
        {
            PlayerActor actor = _player.Actor;
            if (actor.IsDead) { _rollRemaining = 0f; return; }
            Vector3 position = _player.transform.position;
            StateKey state = actor.CurrentState;
            bool canFinishRoll = state == StateKey.Roll || state == StateKey.Idle ||
                state == StateKey.Walk || state == StateKey.Jump;
            if (_rollRemaining > 0f && canFinishRoll)
            {
                float step = Mathf.Min(seconds, _rollRemaining);
                position.x += _rollSpeed * step;
                _rollRemaining -= step;
            }
            else
            {
                _rollRemaining = 0f;
                position.x += actor.Locomotion.HorizontalSpeed / 100f * worldUnitsPerTile * seconds;
            }
            if (_jumping)
            {
                _jumpElapsed += seconds;
                float progress = Mathf.Clamp01(_jumpElapsed / jumpSeconds);
                float height = actor.Definition.JumpHeight / 100f * worldUnitsPerTile;
                position.y = _groundY + 4f * height * progress * (1f - progress);
                if (progress >= 1f)
                {
                    _jumping = false;
                    position.y = _groundY;
                    _player.SetGrounded(true);
                }
            }
            position.x = Mathf.Clamp(position.x, -stageHalfWidth, stageHalfWidth);
            _player.transform.position = position;
        }
    }
}
