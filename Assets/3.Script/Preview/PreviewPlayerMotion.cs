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
        private readonly Action<string> _playSound;
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
        private readonly System.Collections.Generic.List<LadderClimbArea> _ladders = new System.Collections.Generic.List<LadderClimbArea>();
        private LadderClimbArea _ladder;
        private Bounds _lastLadderArea;
        private int _verticalInput, _shownLadderDirection;
        public bool IsOnLadder => !ReferenceEquals(_ladder, null);
        public void SetVerticalInput(int direction) => _verticalInput = Math.Sign(direction);

        // markers: 호출자가 전달한 표식 묶음(매달리기 모서리·낙하 구역).
        public PreviewPlayerMotion(PlayerRuntime player, float groundY, bool useColliders, Vector2 halfSize,
            Transform[] markers, int jumpHeight, int jumpMilliseconds, Action<string> playSound = null)
        {
            _player = player != null ? player : throw new ArgumentNullException(nameof(player));
            _halfSize = halfSize;
            _playSound = playSound;
            _collision = new PreviewCollision(useColliders, groundY, halfSize);
            _hangEdges = new PreviewHangEdges(markers, halfSize);
            _fallZones = new PreviewFallZones(markers);
            foreach (Transform marker in markers ?? Array.Empty<Transform>())
            {
                if (marker == null) continue;
                var ladder = marker.GetComponent<LadderClimbArea>();
                if (ladder != null) _ladders.Add(ladder);
            }
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
            _playSound?.Invoke("PLAYER_JUMP");
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
            if (IsOnLadder)
            {
                bool hanging = _player.Actor.IsHanging;
                bool downward = _verticalInput < 0;
                StopClimbing();
                if (hanging && !_player.Actor.IsDead)
                {
                    if (downward && TryDropThrough()) return;
                    if (downward && !_player.Actor.Locomotion.IsGrounded)
                    {
                        _airborne = true; _coyoteRemaining = 0;
                        _motion.SetVerticalSpeed(-IntegerPosition.UnitsPerTile);
                        return;
                    }
                    _player.SetGrounded(false); StartJump();
                }
                return;
            }
            if (IsHanging) Climb();
            else if (_airborne) _jumpBufferRemaining = _jumpBufferMilliseconds;
        }

        public void OnVerticalPressed(int direction)
        {
            if (!IsHanging) return;
            if (direction > 0) Climb(); else Drop();
        }

        public bool TryUseLadder(int direction)
        {
            if (IsOnLadder) { SetVerticalInput(direction); return true; }
            if (direction == 0 || !_player.Actor.Locomotion.CanMove || _player.Actor.IsDead) return false;
            Vector3 position = _player.transform.position;
            float feet = position.y - _halfSize.y;
            foreach (LadderClimbArea ladder in _ladders)
            {
                if (ladder == null || !ladder.isActiveAndEnabled) continue;
                Bounds area = ladder.Area;
                if (Mathf.Abs(position.x - area.center.x) > area.extents.x + _halfSize.x + 0.1f || feet < area.min.y - 0.12f || feet > area.max.y + 0.12f ||
                    direction > 0 && feet >= area.max.y || direction < 0 && feet <= area.min.y) continue;
                bool grounded = _player.Actor.Locomotion.IsGrounded;
                _player.SetGrounded(false);
                if (!_player.Actor.TryHang()) { _player.SetGrounded(grounded); return false; }
                _ladder = ladder; _lastLadderArea = area; _verticalInput = Math.Sign(direction); _shownLadderDirection = 0;
                position.x = area.center.x; _player.SetPosition(position);
                _motion.StopRoll(); _motion.SetVerticalSpeed(); _airborne = false;
                _jumpBufferRemaining = _coyoteRemaining = 0; _dropRequested = false;
                _peakY = _player.Position.Y;
                _player.NotifyPresentationCue(_verticalInput > 0 ? "LadderUp" : "LadderDown");
                _shownLadderDirection = _verticalInput;
                _playSound?.Invoke("PLAYER_CLIMB");
                return true;
            }
            return false;
        }

        public void StopClimbing() => LeaveLadder(false);
        private void LeaveLadder(bool atTop)
        {
            if (ReferenceEquals(_ladder, null)) return;
            if (_player == null || _player.Actor == null) { _ladder = null; return; }
            Bounds area = _ladder == null ? _lastLadderArea : _ladder.Area;
            _ladder = null; _verticalInput = _shownLadderDirection = 0;
            Vector3 position = _player.transform.position;
            float gap;
            bool grounded = _collision.FindFloor(position, 0.12f, out gap);
            if (!grounded && atTop)
            {
                int preferred = _player.Actor.Locomotion.Direction < 0 ? -1 : 1;
                for (int i = 0; i < 2 && !grounded; i++)
                {
                    Vector3 candidate = position;
                    candidate.x = area.center.x + (i == 0 ? preferred : -preferred) * (area.extents.x + _halfSize.x + PreviewCollision.Skin);
                    if (!_collision.FindFloor(candidate, 0.12f, out gap)) continue;
                    position = candidate; grounded = true;
                }
            }
            if (grounded) { position.y -= gap; _player.SetPosition(position); }
            _player.SetGrounded(grounded);
            _player.Actor.EndHang();
            _motion.SetVerticalSpeed(); _airborne = !grounded; _peakY = _player.Position.Y;
            _player.NotifyPresentationCue("LadderExit");
        }

        private bool MoveLadder(float seconds)
        {
            if (ReferenceEquals(_ladder, null)) return false;
            if (_ladder == null || !_ladder.isActiveAndEnabled || !_player.Actor.IsHanging) { StopClimbing(); return false; }
            Bounds area = _ladder.Area;
            _lastLadderArea = area;
            Vector3 position = _player.transform.position;
            float bottom = area.min.y + _halfSize.y;
            float top = area.max.y + _halfSize.y + PreviewCollision.Skin;
            float step = _verticalInput * _ladder.TilesPerSecond * _player.ToWorld(IntegerPosition.UnitsPerTile) * seconds;
            position.x = area.center.x;
            position.y = Mathf.Clamp(position.y + step, bottom, top);
            _player.SetPosition(position);
            if (_shownLadderDirection != _verticalInput)
            {
                _shownLadderDirection = _verticalInput;
                _player.NotifyPresentationCue(_verticalInput > 0 ? "LadderUp" : _verticalInput < 0 ? "LadderDown" : "LadderHold");
            }
            if (_verticalInput > 0 && position.y >= top || _verticalInput < 0 && position.y <= bottom) LeaveLadder(_verticalInput > 0);
            return true;
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
            _player.NotifyPresentationCue("Climb");
            _playSound?.Invoke("PLAYER_CLIMB");
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
            if (MoveLadder(milliseconds / 1000f)) return;
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
            if (_airborne) _playSound?.Invoke("PLAYER_LAND");
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
