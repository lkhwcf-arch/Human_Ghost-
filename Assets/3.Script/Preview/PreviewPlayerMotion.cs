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
        // K04 R6·K16add R9·L01 R13: 낙사는 HP와 무관한 즉사입니다.
        private const int _fallDamage = int.MaxValue;
        private readonly PlayerRuntime _player;
        private readonly Action<string> _playSound;
        private readonly PreviewCollision _collision;
        private readonly PreviewHangEdges _hangEdges;
        private readonly PreviewFallZones _fallZones;
        // [정수화 미적용 이유] 플레이어 충돌 상자 절반 크기입니다. 플레이어 충돌 상자·이동은 float Physics2D 계약(비탈 보류, 2026-11-17)을 유지하므로 이 경계에서 실수로 둡니다.
        private readonly Vector2 _halfSize;
        private readonly IntegerMotion _motion = new IntegerMotion();
        private readonly IntegerMotion _knockback = new IntegerMotion();
        private readonly IntegerMotion _ladderMotion = new IntegerMotion();
        private MillisecondClock _clock;
        private int _coyoteRemaining;
        private int _jumpBufferRemaining;
        private bool _dropRequested;
        private long _peakY;
        private bool _airborne;
        private Transform _hangEdge;
        private Transform _releasedEdge;
        private int _hangSide;
        private const int _ledgeClimbMilliseconds = 1000;
        private int _climbElapsed = -1;
        private IntegerPosition _climbFrom, _climbTo;
        // 턱 오르기 보간 구간입니다(분모 10000의 진행률). 위로 끌어올리기 15~80%, 앞으로 넘어가기 45~95%(기존 0.15+0.65·0.45+0.5).
        private const int _rateScale = 10000;
        private const int _climbRiseStartRate = 1500, _climbRiseLengthRate = 6500;
        private const int _climbAdvanceStartRate = 4500, _climbAdvanceLengthRate = 5000;
        public bool IsClimbingLedge => _climbElapsed >= 0;
        // [정수화 미적용 이유] 보호 중인 턱 표식의 Transform 위치입니다. 받는 쪽(PreviewMonsterDriver)이 즉시 정수로 바꿉니다.
        public Vector2? ProtectedLedge => _player.Actor.IsTraversalProtected && _hangEdge != null ? (Vector2?)_hangEdge.position : null;
        private readonly System.Collections.Generic.List<LadderClimbArea> _ladders = new System.Collections.Generic.List<LadderClimbArea>();
        private LadderClimbArea _ladder;
        private const int _ladderStepMilliseconds = 350;
        private int _ladderStepRemaining;
        private Bounds _lastLadderArea;
        private int _verticalInput, _shownLadderDirection;
        public bool IsOnLadder => !ReferenceEquals(_ladder, null);
        public void SetVerticalInput(int direction) => _verticalInput = Math.Sign(direction);

        // markers: 호출자가 전달한 표식 묶음(매달리기 모서리·낙하 구역).
        public PreviewPlayerMotion(PlayerRuntime player, float groundY, bool useColliders, Vector2 halfSize,
            Transform[] markers, int jumpHeight, int jumpMilliseconds, Action<string> playSound = null, Transform dropThroughRoot = null)
        {
            _player = player != null ? player : throw new ArgumentNullException(nameof(player));
            _halfSize = halfSize;
            _playSound = playSound;
            _collision = new PreviewCollision(useColliders, groundY, halfSize, dropThroughRoot);
            _hangEdges = new PreviewHangEdges(markers, halfSize, player);
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
            if (_airborne) { _dropRequested = true; return false; }
            if (!_player.Actor.Locomotion.IsGrounded || !_collision.TryBeginDrop(_player.transform.position)) return false;
            _motion.SetVerticalSpeed(-IntegerPosition.UnitsPerTile);
            _airborne = true;
            _coyoteRemaining = 0;
            _jumpBufferRemaining = 0;
            _dropRequested = false;
            _peakY = _player.Position.Y;
            _player.SetGrounded(false);
            _player.NotifyPresentationCue("DropThrough");
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

        public void StartKnockback(int direction, int distance, int duration)
        {
            if (_player.Actor.IsDead) return;
            StopClimbing();
            DiscardBufferedInput();
            _motion.StopRoll();
            _knockback.StartRoll(direction, distance, duration);
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
            if (!IsHanging || IsClimbingLedge) return;
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
                _ladderMotion.ResetHorizontal();
                position.x += _collision.MoveHorizontal(position, _ladder.AxisX - position.x); _player.SetPosition(position);
                _motion.StopRoll(); _motion.SetVerticalSpeed(); _airborne = false;
                _jumpBufferRemaining = _coyoteRemaining = 0; _dropRequested = false;
                _peakY = _player.Position.Y;
                _player.NotifyPresentationCue(_verticalInput > 0 ? "LadderUp" : "LadderDown");
                _shownLadderDirection = _verticalInput;
                // 사다리 전용 음원이 없어 발소리를 칸 단위로 씁니다. 모서리 매달리기·오르기 소리는 쓰지 않습니다.
                _ladderStepRemaining = 0;
                return true;
            }
            return false;
        }

        public void StopClimbing() { CancelLedgeClimb(); LeaveLadder(false); }

        // 보간 내부 정밀도(백만분율)입니다. 진행률을 1/10000로 자르면 위치 오차가 0.003칸까지 생겨 더 촘촘히 계산합니다.
        private const long _phaseScale = 1000000;

        // 경과 ms 중 [start, start+length] 구간(분모 10000의 비율)의 진행을 0~1,000,000으로 고정해 돌려줍니다.
        private static long PhaseRate(int elapsedMilliseconds, int startRate, int lengthRate)
        {
            long offset = (long)elapsedMilliseconds * _rateScale - (long)startRate * _ledgeClimbMilliseconds;
            return Math.Max(0L, Math.Min(_phaseScale, offset * _phaseScale / ((long)lengthRate * _ledgeClimbMilliseconds)));
        }

        // Mathf.SmoothStep(from, to, t)의 정수 판입니다. t = rate/1,000,000일 때 from + (to-from)·t²(3-2t)이며 끝점은 정확히 from·to입니다.
        private static long SmoothStep(long from, long to, long rate)
        {
            long eased = rate * rate / _phaseScale * (3L * _phaseScale - 2L * rate) / _phaseScale;
            return checked(from + (to - from) * eased / _phaseScale);
        }

        private void CancelLedgeClimb()
        {
            if (!IsClimbingLedge) return;
            _climbElapsed = -1; _player.SetClimbProgress(-1);
            _player.NotifyPresentationCue("ClimbExit");
        }
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
                    if (Mathf.Abs(_collision.MoveHorizontal(position, candidate.x - position.x) - (candidate.x - position.x)) > PreviewCollision.Skin ||
                        !_collision.FindFloor(candidate, 0.12f, out gap)) continue;
                    position = candidate; grounded = true;
                }
            }
            if (grounded) { position.y -= gap; _player.SetPosition(position); }
            _player.SetGrounded(grounded);
            _player.Actor.EndHang();
            _motion.SetVerticalSpeed(); _airborne = !grounded; _peakY = _player.Position.Y;
            _player.NotifyPresentationCue("LadderExit");
        }

        private bool MoveLadder(int milliseconds)
        {
            if (ReferenceEquals(_ladder, null)) return false;
            if (_ladder == null || !_ladder.isActiveAndEnabled || !_player.Actor.IsHanging) { StopClimbing(); return false; }
            Bounds area = _ladder.Area;
            _lastLadderArea = area;
            Vector3 position = _player.transform.position;
            long bottom = _player.ToInteger(area.min.y + _halfSize.y);
            long top = _player.ToInteger(area.max.y + _halfSize.y + PreviewCollision.Skin);
            long step = _ladderMotion.StepHorizontal(checked(_verticalInput * _ladder.SpeedUnitsPerSecond), milliseconds);
            position.x += _collision.MoveHorizontal(position, _ladder.AxisX - position.x);
            long nextY = checked(_player.Position.Y + step);
            nextY = Math.Max(bottom, Math.Min(top, nextY));
            // 프레임별 정수 이동량이 0이어도 입력 중에는 이동으로 봅니다(끝에 닿으면 정지).
            bool moved = _verticalInput > 0 ? _player.Position.Y < top : _verticalInput < 0 && _player.Position.Y > bottom;
            position.y = _player.ToWorld(nextY);
            _player.SetPosition(position);
            if (moved && (_ladderStepRemaining -= milliseconds) <= 0)
            {
                _playSound?.Invoke("PLAYER_WALK");
                _ladderStepRemaining = _ladderStepMilliseconds;
            }
            else if (!moved) _ladderStepRemaining = 0;
            if (_shownLadderDirection != _verticalInput)
            {
                _shownLadderDirection = _verticalInput;
                _player.NotifyPresentationCue(_verticalInput > 0 ? "LadderUp" : _verticalInput < 0 ? "LadderDown" : "LadderHold");
            }
            if (_verticalInput > 0 && nextY == top || _verticalInput < 0 && nextY == bottom) LeaveLadder(_verticalInput > 0);
            return true;
        }

        public bool CanGrab => !_player.Actor.Definition.HangAuto && _airborne && !IsHanging &&
            _hangEdges.TryFind(_player.transform.position, out _, out _, _releasedEdge);

        public bool TryGrab()
        {
            if (!_airborne || IsHanging) return false;
            Vector3 position = _player.transform.position;
            if (!_hangEdges.TryFind(position, out Transform edge, out int side, _releasedEdge) || !_player.Actor.TryHang(side, true)) return false;
            _playSound?.Invoke("PLAYER_HANG");
            _hangEdge = edge; _hangSide = side;
            _motion.StopRoll(); _motion.SetVerticalSpeed();
            _player.SetPosition(_hangEdges.HangPosition(edge, side, position.z));
            return true;
        }

        private void Climb()
        {
            if (IsClimbingLedge || !IsHanging || !_player.Actor.IsHanging) return;
            _climbFrom = _player.Position;
            // Unity 경계: 턱 표식(Transform)에서 구한 도착 위치는 읽는 즉시 정수로 바꿉니다.
            _climbTo = _player.FromWorld(_hangEdges.ClimbPosition(_hangEdge, _hangSide, _player.transform.position.z));
            _climbElapsed = 0; _player.SetClimbProgress(0);
            _motion.StopRoll(); _motion.SetVerticalSpeed();
            _player.NotifyPresentationCue("Climb");
            _playSound?.Invoke("PLAYER_CLIMB");
        }

        private void Drop()
        {
            _releasedEdge = _hangEdge;
            _hangEdge = null;
            _airborne = true; _motion.SetVerticalSpeed();
            _peakY = _player.Position.Y;
            _player.Actor.EndHang();
            _player.NotifyPresentationCue("DropThrough");
        }

        public void Move(float seconds, float minX, float maxX)
        {
            if (_player.Actor.CurrentState == StateKey.Execute) return;
            int milliseconds = _clock.AdvanceFrame(seconds);
            if (milliseconds == 0) return;
            PlayerActor actor = _player.Actor;
            if (IsClimbingLedge)
            {
                if (!actor.IsHanging || _hangEdge == null)
                {
                    CancelLedgeClimb(); _hangEdge = null; _airborne = true; _motion.SetVerticalSpeed();
                    _peakY = _player.Position.Y;
                }
                else
                {
                    _climbElapsed += Math.Min(milliseconds, _ledgeClimbMilliseconds - _climbElapsed);
                    int progress = (int)((long)_climbElapsed * _rateScale / _ledgeClimbMilliseconds);
                    // 위로 끌어올리기와 앞으로 넘어가기를 겹치고 양 끝을 감속해 꺾임 없이 이어지게 합니다(정수 SmoothStep).
                    long climbY = SmoothStep(_climbFrom.Y, _climbTo.Y, PhaseRate(_climbElapsed, _climbRiseStartRate, _climbRiseLengthRate));
                    long climbX = SmoothStep(_climbFrom.X, _climbTo.X, PhaseRate(_climbElapsed, _climbAdvanceStartRate, _climbAdvanceLengthRate));
                    _player.SetPosition(new IntegerPosition(climbX, climbY)); _player.SetClimbProgress(progress);
                    if (_climbElapsed == _ledgeClimbMilliseconds)
                    {
                        CancelLedgeClimb(); _hangEdge = null;
                        _airborne = false; _coyoteRemaining = _jumpBufferRemaining = 0; _dropRequested = false;
                        _player.SetGrounded(true); actor.EndHang();
                    }
                    return;
                }
            }
            if (MoveLadder(milliseconds)) return;
            if (IsHanging)
            {
                if (actor.IsHanging) return;
                _hangEdge = null; _airborne = true; _motion.SetVerticalSpeed();
                _peakY = _player.Position.Y;
            }
            if (actor.IsDead) { _motion.StopRoll(); _knockback.StopRoll(); }
            IntegerPosition previous = _player.Position;
            Vector3 position = _player.ToWorld(previous);
            float previousFeet = position.y - _halfSize.y;
            long delta = GetHorizontalDelta(actor, milliseconds);
            float requested = _player.ToWorld(delta);
            float targetX = position.x + requested;
            float slopeHeight = position.y;
            // [정수 경사 규칙 미적용 이유] 2026-11-17 사용자 결정으로 플레이어 비탈 개선은 보류입니다. 기존 몸통 폭·실수 비탈 추종을 그대로 씁니다.
            // 적용할 때는 몬스터와 같은 PreviewFootCollision(정수 TryFollowFootSlope)을 재사용합니다.
            bool onSlope = !_airborne && targetX >= minX && targetX <= maxX &&
                _collision.TryFollowGroundSlope(position, requested, out slopeHeight);
            float allowed = onSlope ? requested : _collision.MoveHorizontal(position, requested);
            long x = checked(previous.X + (allowed == requested ? delta : _player.ToInteger(allowed)));
            if (!float.IsNegativeInfinity(minX)) x = Math.Max(x, _player.ToInteger(minX));
            if (!float.IsPositiveInfinity(maxX)) x = Math.Min(x, _player.ToInteger(maxX));
            if (allowed != requested || x != previous.X + delta) { _motion.ResetHorizontal(); _knockback.StopRoll(); }
            position.x = _player.ToWorld(x);
            long y = onSlope ? _player.ToInteger(slopeHeight) : MoveVertical(position, previous.Y, milliseconds);
            if (onSlope) _coyoteRemaining = 0;
            _player.SetPosition(new IntegerPosition(x, y));
            position = _player.ToWorld(_player.Position);
            _peakY = Math.Max(_peakY, y);
            _jumpBufferRemaining = Math.Max(0, _jumpBufferRemaining - milliseconds);
            if (_jumpBufferRemaining == 0) _dropRequested = false;
            if (_fallZones.TryHit(position.x, previousFeet, position.y - _halfSize.y, out Vector3 respawn)) ApplyFall(respawn, position.z);
            // 놓은 턱의 잡기 범위를 벗어난 뒤에만 같은 턱을 다시 잡습니다. 다른 턱은 허용합니다.
            if (_releasedEdge != null && !_hangEdges.IsInReach(position, _releasedEdge)) _releasedEdge = null;
            if (actor.Definition.HangAuto) TryGrab();
        }

        private long GetHorizontalDelta(PlayerActor actor, int milliseconds)
        {
            if (_knockback.IsRolling) return _knockback.StepRoll(milliseconds);
            StateKey state = actor.CurrentState;
            bool canFinishRoll = state == StateKey.Roll || state == StateKey.Skill1 || state == StateKey.Idle || state == StateKey.Walk || state == StateKey.Jump;
            if (_motion.IsRolling && canFinishRoll) return _motion.StepRoll(milliseconds);
            _motion.StopRoll();
            if (actor.IsDead) return 0;
            int speed = actor.HorizontalMoveSpeed;
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
                _player.NotifyPresentationCue("DropThrough");
                return landY - IntegerPosition.FromData(1);
            }
            _dropRequested = false;
            Land(Math.Max(0, _peakY - landY));
            return landY;
        }

        private void Land(long fallUnits)
        {
            bool airAttack = _player.Actor.IsAirAttacking;
            if (_airborne) _playSound?.Invoke("PLAYER_LAND");
            bool coreKnewAirborne = _coyoteRemaining <= 0;
            _airborne = false; _motion.SetVerticalSpeed(); _coyoteRemaining = 0;
            if (coreKnewAirborne) _player.SetGrounded(true);
            if (_jumpBufferRemaining > 0 && _player.Actor.TryJump()) { StartJump(); return; }
            if (!airAttack) _player.Actor.NotifyLanded(IntegerPosition.ToData(fallUnits));
        }

        // 구덩이에 닿으면 즉사해 기존 사망 흐름으로 넘깁니다. 컷씬 피해 차단 중에만 구덩이 앞에서 다시 섭니다(암전 300ms는 L08 가정).
        private void ApplyFall(Vector3 respawn, float z)
        {
            if (_player.Actor.ApplyFall(_fallDamage)) return;
            respawn.z = z; respawn.y += _halfSize.y;
            _motion.StopRoll();
            _player.SetPosition(respawn);
            _airborne = false; _motion.SetVerticalSpeed(); _coyoteRemaining = 0;
            _player.SetGrounded(true);
            PreviewScreenFade.Run(100, 100, 100, null);
        }
    }
}
