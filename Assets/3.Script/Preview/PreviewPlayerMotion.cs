using System;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay.Adapters;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 확인 장면 전용 이동 조정입니다. 조회·모서리·낙하 구역은 각 모듈에 맡기며 운영 물리를 결정하지 않습니다.
    internal sealed class PreviewPlayerMotion
    {
        private const float _coyoteSeconds = 0.1f;
        private const float _jumpBufferSeconds = 0.1f;
        private const float _maxFallSpeed = 30f;
        private const int _fallDamage = 10;
        private readonly PlayerRuntime _player;
        private readonly PreviewCollision _collision;
        private readonly PreviewHangEdges _hangEdges;
        private readonly PreviewFallZones _fallZones;
        private readonly Vector2 _halfSize;
        private readonly float _gravity;
        private readonly float _jumpSpeed;
        private float _rollRemaining;
        private float _rollSpeed;
        private float _verticalSpeed;
        private float _coyoteRemaining;
        private float _jumpBufferRemaining;
        private float _peakY;
        private bool _airborne;
        private Transform _hangEdge;
        private int _hangSide;

        // markers: 호출자가 전달한 표식 묶음(매달리기 모서리·낙하 구역).
        public PreviewPlayerMotion(PlayerRuntime player, float groundY, bool useColliders, Vector2 halfSize,
            Transform[] markers, float jumpHeightTiles, float jumpSeconds)
        {
            _player = player != null ? player : throw new ArgumentNullException(nameof(player));
            _halfSize = halfSize;
            _collision = new PreviewCollision(useColliders, groundY, halfSize);
            _hangEdges = new PreviewHangEdges(markers, halfSize);
            _fallZones = new PreviewFallZones(markers);
            // 기존 임시 점프와 같은 높이·시간의 포물선입니다.
            _jumpSpeed = 4f * jumpHeightTiles / jumpSeconds;
            _gravity = 8f * jumpHeightTiles / (jumpSeconds * jumpSeconds);
        }

        public bool IsHanging => _hangEdge != null;

        public void DiscardBufferedInput() => _jumpBufferRemaining = 0f;

        public void StartJump()
        {
            _verticalSpeed = _jumpSpeed;
            _airborne = true;
            _coyoteRemaining = 0f;
            _jumpBufferRemaining = 0f;
            _peakY = _player.transform.position.y;
        }

        public void StartRoll(int direction, int distance, int duration, float worldUnitsPerTile)
        {
            _rollRemaining = duration / 1000f;
            _rollSpeed = duration > 0 ? direction * distance / 100f * worldUnitsPerTile / _rollRemaining : 0f;
        }

        // 공중 점프 입력은 착지 직전 선입력으로, 매달린 중에는 올라가기로 씁니다.
        public void OnJumpRejected()
        {
            if (IsHanging) Climb();
            else if (_airborne) _jumpBufferRemaining = _jumpBufferSeconds;
        }

        public void OnVerticalPressed(int direction)
        {
            if (!IsHanging) return;
            if (direction > 0) Climb(); else Drop();
        }

        public bool CanGrab => _airborne && !IsHanging && _hangEdges.TryFind(_player.transform.position, out _, out _);

        public bool TryGrab()
        {
            if (!_airborne || IsHanging) return false;
            Vector3 position = _player.transform.position;
            if (!_hangEdges.TryFind(position, out Transform edge, out int side) || !_player.Actor.TryHang()) return false;
            _hangEdge = edge; _hangSide = side;
            _rollRemaining = 0f; _verticalSpeed = 0f;
            _player.SetPosition(_hangEdges.HangPosition(edge, side, position.z));
            return true;
        }

        private void Climb()
        {
            _player.SetPosition(_hangEdges.ClimbPosition(_hangEdge, _hangSide, _player.transform.position.z));
            _hangEdge = null;
            _airborne = false; _verticalSpeed = 0f;
            _player.SetGrounded(true);
            _player.Actor.EndHang();
        }

        private void Drop()
        {
            _hangEdge = null;
            _airborne = true; _verticalSpeed = 0f;
            _peakY = _player.transform.position.y;
            _player.Actor.EndHang();
        }

        public void Move(float seconds, float worldUnitsPerTile, float minX, float maxX)
        {
            PlayerActor actor = _player.Actor;
            if (IsHanging)
            {
                // 맞으면 코어가 Hang을 끝냅니다. 그때부터 떨어집니다.
                if (actor.IsHanging) return;
                _hangEdge = null; _airborne = true; _verticalSpeed = 0f;
                _peakY = _player.transform.position.y;
            }
            if (actor.IsDead) _rollRemaining = 0f;
            Vector3 position = _player.transform.position;
            float previousFeet = position.y - _halfSize.y;
            position.x += _collision.MoveHorizontal(position, GetHorizontalDelta(actor, seconds, worldUnitsPerTile));
            position.x = Mathf.Clamp(position.x, minX, maxX);
            position.y = MoveVertical(position, seconds);
            _player.SetPosition(position);
            _peakY = Mathf.Max(_peakY, position.y);
            _jumpBufferRemaining = Mathf.Max(0f, _jumpBufferRemaining - seconds);
            if (_fallZones.TryHit(position.x, previousFeet, position.y - _halfSize.y, out Vector3 respawn)) ApplyFall(respawn, position.z);
        }

        private float GetHorizontalDelta(PlayerActor actor, float seconds, float worldUnitsPerTile)
        {
            StateKey state = actor.CurrentState;
            bool canFinishRoll = state == StateKey.Roll || state == StateKey.Skill1 || state == StateKey.Idle || state == StateKey.Walk || state == StateKey.Jump;
            if (_rollRemaining > 0f && canFinishRoll)
            {
                float step = Mathf.Min(seconds, _rollRemaining);
                _rollRemaining -= step;
                return _rollSpeed * step;
            }
            _rollRemaining = 0f;
            if (actor.IsDead) return 0f;
            int speed = state == StateKey.Heal ? actor.HealMoveSpeed : actor.Locomotion.HorizontalSpeed;
            return speed / 100f * worldUnitsPerTile * seconds;
        }

        private float MoveVertical(Vector3 position, float seconds)
        {
            if (!_airborne)
            {
                if (_collision.FindFloor(position, 0.05f, out _)) { _coyoteRemaining = 0f; return position.y; }
                // 발판에서 떨어지면 바로 낙하하되 코어 공중 전환은 잠시 늦춰 늦은 점프를 받습니다.
                _airborne = true; _verticalSpeed = 0f;
                _coyoteRemaining = _coyoteSeconds;
                _peakY = position.y;
            }
            if (_coyoteRemaining > 0f)
            {
                _coyoteRemaining -= seconds;
                if (_coyoteRemaining <= 0f) _player.SetGrounded(false);
            }
            _verticalSpeed = Mathf.Max(_verticalSpeed - _gravity * seconds, -_maxFallSpeed);
            float delta = _verticalSpeed * seconds;
            if (delta > 0f)
            {
                float moved = _collision.MoveUp(position, delta, out bool hitCeiling);
                if (hitCeiling) _verticalSpeed = 0f;
                return position.y + moved;
            }
            float distance = -delta;
            if (!_collision.FindFloor(position, distance, out float floorDistance)) return position.y - distance;
            float landY = position.y - floorDistance;
            Land(_peakY - landY);
            return landY;
        }

        private void Land(float fallTiles)
        {
            bool coreKnewAirborne = _coyoteRemaining <= 0f;
            _airborne = false; _verticalSpeed = 0f; _coyoteRemaining = 0f;
            if (coreKnewAirborne) _player.SetGrounded(true);
            if (_jumpBufferRemaining > 0f && _player.Actor.TryJump()) { StartJump(); return; }
            _player.Actor.NotifyLanded(Mathf.RoundToInt(fallTiles * 100f));
        }

        // 구덩이 앞에서 다시 섭니다(HP 10 감소, 10 이하면 낙사). 암전 300ms는 L08 가정입니다.
        private void ApplyFall(Vector3 respawn, float z)
        {
            if (_player.Actor.ApplyFall(_fallDamage)) return;
            respawn.z = z; respawn.y += _halfSize.y;
            _rollRemaining = 0f;
            _player.SetPosition(respawn);
            _airborne = false; _verticalSpeed = 0f; _coyoteRemaining = 0f;
            _player.SetGrounded(true);
            PreviewScreenFade.Run(0.1f, 0.1f, 0.1f, null);
        }
    }
}
