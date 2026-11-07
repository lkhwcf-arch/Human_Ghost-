using System;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay.Adapters;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 확인 장면 전용 이동 조정입니다. 조회·모서리·낙하 구역은 각 모듈에 맡기며 운영 물리를 결정하지 않습니다.
    internal sealed class PreviewPlayerMotion
    {
        private const int _coyoteMilliseconds = 100;
        private const int _jumpBufferMilliseconds = 100;
        private const int _fallDamage = 10;
        private readonly PlayerRuntime _player;
        private readonly PreviewCollision _collision;
        private readonly PreviewHangEdges _hangEdges;
        private readonly PreviewFallZones _fallZones;
        private readonly Vector2 _halfSize;
        private readonly IntegerMotion _motion = new IntegerMotion();
        private MillisecondClock _clock;
        private int _coyoteRemaining;
        private int _jumpBufferRemaining;
        private bool _dropRequested;
        private long _peakY;
        private bool _airborne;
        private Transform _hangEdge;
        private int _hangSide;

        // markers: 호출자가 전달한 표식 묶음(매달리기 모서리·낙하 구역).
        public PreviewPlayerMotion(PlayerRuntime player, float groundY, bool useColliders, Vector2 halfSize,
            Transform[] markers, int jumpHeight, int jumpMilliseconds)
        {
            _player = player != null ? player : throw new ArgumentNullException(nameof(player));
            _halfSize = halfSize;
            _collision = new PreviewCollision(useColliders, groundY, halfSize);
            _hangEdges = new PreviewHangEdges(markers, halfSize);
            _fallZones = new PreviewFallZones(markers);
            _motion.ConfigureJump(IntegerPosition.FromData(jumpHeight), jumpMilliseconds);
        }

        public bool IsHanging => _hangEdge != null;

        public bool TryDropThrough()
        {
            if (IsHanging || _player.Actor.IsDead || !_player.Actor.Locomotion.CanMove) return false;
            if (_airborne && !IsHanging) { _dropRequested = true; return false; }
            if (!_player.Actor.Locomotion.IsGrounded || !_collision.TryBeginDrop(_player.transform.position)) return false;
            _motion.SetVerticalSpeed(-IntegerPosition.UnitsPerTile);
            _airborne = true;
            _coyoteRemaining = 0;
            _jumpBufferRemaining = 0;
            _dropRequested = false;
            _peakY = _player.Position.Y;
            _player.SetGrounded(false);
            return true;
        }

        public void DiscardBufferedInput() { _jumpBufferRemaining = 0; _dropRequested = false; }

        public void StartJump()
        {
            _motion.StartJump();
            _airborne = true;
            _coyoteRemaining = 0;
            _jumpBufferRemaining = 0;
            _dropRequested = false;
            _peakY = _player.Position.Y;
        }

        public void StartRoll(int direction, int distance, int duration)
        {
            if (duration > 0) _motion.StartRoll(direction, distance, duration);
            else _motion.StopRoll();
        }

        // 공중 점프 입력은 착지 직전 선입력으로, 매달린 중에는 올라가기로 씁니다.
        public void OnJumpRejected()
        {
            if (IsHanging) Climb();
            else if (_airborne) _jumpBufferRemaining = _jumpBufferMilliseconds;
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
            _motion.StopRoll(); _motion.SetVerticalSpeed();
            _player.SetPosition(_hangEdges.HangPosition(edge, side, position.z));
            return true;
        }

        private void Climb()
        {
            _player.SetPosition(_hangEdges.ClimbPosition(_hangEdge, _hangSide, _player.transform.position.z));
            _hangEdge = null;
            _airborne = false; _motion.SetVerticalSpeed();
            _player.SetGrounded(true);
            _player.Actor.EndHang();
        }

        private void Drop()
        {
            _hangEdge = null;
            _airborne = true; _motion.SetVerticalSpeed();
            _peakY = _player.Position.Y;
            _player.Actor.EndHang();
        }

        public void Move(float seconds, float minX, float maxX)
        {
            int milliseconds = _clock.Advance(seconds);
            if (milliseconds == 0) return;
            PlayerActor actor = _player.Actor;
            if (IsHanging)
            {
                if (actor.IsHanging) return;
                _hangEdge = null; _airborne = true; _motion.SetVerticalSpeed();
                _peakY = _player.Position.Y;
            }
            if (actor.IsDead) _motion.StopRoll();
            IntegerPosition previous = _player.Position;
            Vector3 position = _player.ToWorld(previous);
            float previousFeet = position.y - _halfSize.y;
            long delta = GetHorizontalDelta(actor, milliseconds);
            float requested = _player.ToWorld(delta);
            float allowed = _collision.MoveHorizontal(position, requested);
            long x = checked(previous.X + (allowed == requested ? delta : _player.ToInteger(allowed)));
            if (!float.IsNegativeInfinity(minX)) x = Math.Max(x, _player.ToInteger(minX));
            if (!float.IsPositiveInfinity(maxX)) x = Math.Min(x, _player.ToInteger(maxX));
            if (allowed != requested || x != previous.X + delta) _motion.ResetHorizontal();
            position.x = _player.ToWorld(x);
            long y = MoveVertical(position, previous.Y, milliseconds);
            _player.SetPosition(new IntegerPosition(x, y));
            position = _player.ToWorld(_player.Position);
            _peakY = Math.Max(_peakY, y);
            _jumpBufferRemaining = Math.Max(0, _jumpBufferRemaining - milliseconds);
            if (_jumpBufferRemaining == 0) _dropRequested = false;
            if (_fallZones.TryHit(position.x, previousFeet, position.y - _halfSize.y, out Vector3 respawn)) ApplyFall(respawn, position.z);
        }

        private long GetHorizontalDelta(PlayerActor actor, int milliseconds)
        {
            StateKey state = actor.CurrentState;
            bool canFinishRoll = state == StateKey.Roll || state == StateKey.Skill1 || state == StateKey.Idle || state == StateKey.Walk || state == StateKey.Jump;
            if (_motion.IsRolling && canFinishRoll) return _motion.StepRoll(milliseconds);
            _motion.StopRoll();
            if (actor.IsDead) return 0;
            int speed = state == StateKey.Heal ? actor.HealMoveSpeed : actor.Locomotion.HorizontalSpeed;
            return _motion.StepHorizontal(speed, milliseconds);
        }

        private long MoveVertical(Vector3 position, long currentY, int milliseconds)
        {
            if (!_airborne)
            {
                if (_collision.FindFloor(position, 0.05f, out _)) { _coyoteRemaining = 0; return currentY; }
                _airborne = true; _motion.SetVerticalSpeed();
                _coyoteRemaining = _coyoteMilliseconds;
                _peakY = currentY;
            }
            if (_coyoteRemaining > 0)
            {
                _coyoteRemaining = Math.Max(0, _coyoteRemaining - milliseconds);
                if (_coyoteRemaining == 0) _player.SetGrounded(false);
            }
            long delta = _motion.StepVertical(milliseconds);
            float worldDelta = _player.ToWorld(delta);
            if (delta > 0)
            {
                float moved = _collision.MoveUp(position, worldDelta, out bool hitCeiling);
                if (hitCeiling) { _motion.SetVerticalSpeed(); return checked(currentY + _player.ToInteger(moved)); }
                return checked(currentY + delta);
            }
            if (!_collision.FindFloor(position, -worldDelta, out float floorDistance)) return checked(currentY + delta);
            long landY = checked(currentY - _player.ToInteger(floorDistance));
            if (_jumpBufferRemaining > 0 && _dropRequested && !_player.Actor.IsDead && _player.Actor.Locomotion.CanMove &&
                _collision.TryBeginDrop(new Vector3(position.x, _player.ToWorld(landY), position.z)))
            {
                _dropRequested = false; _jumpBufferRemaining = 0;
                _coyoteRemaining = 0; _motion.SetVerticalSpeed(-IntegerPosition.UnitsPerTile);
                _player.SetGrounded(false);
                return landY - IntegerPosition.FromData(1);
            }
            _dropRequested = false;
            Land(Math.Max(0, _peakY - landY));
            return landY;
        }

        private void Land(long fallUnits)
        {
            bool coreKnewAirborne = _coyoteRemaining <= 0;
            _airborne = false; _motion.SetVerticalSpeed(); _coyoteRemaining = 0;
            if (coreKnewAirborne) _player.SetGrounded(true);
            if (_jumpBufferRemaining > 0 && _player.Actor.TryJump()) { StartJump(); return; }
            _player.Actor.NotifyLanded(IntegerPosition.ToData(fallUnits));
        }

        // 구덩이 앞에서 다시 섭니다(HP 10 감소, 10 이하면 낙사). 암전 300ms는 L08 가정입니다.
        private void ApplyFall(Vector3 respawn, float z)
        {
            if (_player.Actor.ApplyFall(_fallDamage)) return;
            respawn.z = z; respawn.y += _halfSize.y;
            _motion.StopRoll();
            _player.SetPosition(respawn);
            _airborne = false; _motion.SetVerticalSpeed(); _coyoteRemaining = 0;
            _player.SetGrounded(true);
            PreviewScreenFade.Run(0.1f, 0.1f, 0.1f, null);
        }
    }
}
