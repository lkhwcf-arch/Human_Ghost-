using System;
using HumanGhost.Gameplay.Adapters;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 테스트씬 전용 순환 선택·이동·접촉·사슬 끌기. 운영 AI·물리는 담당하지 않습니다.
    internal sealed class PreviewBossDriver : IDisposable
    {
        private readonly BossRuntime _runtime;
        private readonly PlayerRuntime _player;
        private readonly SpriteRenderer _body;
        private readonly SpriteRenderer _playerBody;
        private readonly int _rangeUnits;
        private readonly int _secondRangeUnits;

        private int _nextPattern;
        private readonly BossPhaseTwoSelector _phaseTwo;
        private readonly BossCombat _combat;
        private readonly SubscriptionScope _scope = new SubscriptionScope();
        private int _platformMilliseconds, _preparedHit = -1;
        private BossPattern _preparedPattern;
        // 이동 범위(몸 폭 반영)·도약 시작·착지 X는 정수 좌표입니다. Transform 대입·표시로 넘길 때만 월드로 바꿉니다(S11).
        private long _minX, _maxX, _landingX, _leapStartX;
        // 바닥 예고 그림의 기준 X(월드)입니다. 표시 경계에서만 변환합니다.
        public float WarningOriginX
        {
            get
            {
                var combat = _runtime.Combat; var pattern = combat.CurrentPattern;
                if (pattern?.Kind == BossPatternKind.DesperationLeap && combat.IsAirborne) return _runtime.ToWorld(_landingX);
                long x = _runtime.Position.X;
                // 3연타는 판정 창이 열릴 때 한 칸 전진합니다. 경고도 같은 최종 위치를 보여 줍니다.
                if (pattern?.Kind == BossPatternKind.TripleSlam && combat.IsWindingUp &&
                    (_preparedPattern != pattern || _preparedHit != combat.HitIndex))
                    x = Math.Max(_minX, Math.Min(_maxX, checked(x + combat.Facing * IntegerPosition.UnitsPerTile)));
                return _runtime.ToWorld(x);
            }
        }
        // 플레이어가 보스 발보다 이만큼 높으면(1.5칸) 발판 위 대기 시간을 셉니다.
        private const long _platformHeight = IntegerPosition.UnitsPerTile * 3 / 2;
        private MillisecondClock _clock;
        private readonly IntegerMotion _motion = new IntegerMotion();
        private readonly IntegerMotion _pullMotion = new IntegerMotion();
        public event Action PlayerHit;
        public event Action<bool> HitResolving;
        public event Action PlayerDodged;
        // 추적 중이거나 패턴을 쓰는 동안입니다. 전투 BGM·HUD에 씁니다.
        public bool IsEngaged { get; private set; }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 시험창에서 자동 선택·추적만 제어합니다. 선택한 패턴의 실행은 유지합니다.
        public bool AutomaticPatterns { get; set; } = true;
#else
        // 개발 빌드의 공개 시험 속성과 같은 이름을 써야 공통 코드가 그대로 컴파일되므로 private 상수 명명 규칙의 예외입니다.
        private const bool AutomaticPatterns = true;
#endif

        public PreviewBossDriver(BossRuntime runtime, PlayerRuntime player, SpriteRenderer body,
            SpriteRenderer playerBody, float worldUnitsPerTile, int rangeUnits, int secondRangeUnits, int sightUnits)
        {
            _runtime = runtime != null ? runtime : throw new ArgumentNullException(nameof(runtime));
            _player = player != null ? player : throw new ArgumentNullException(nameof(player));
            _body = body != null ? body : throw new ArgumentNullException(nameof(body));
            _playerBody = playerBody != null ? playerBody : throw new ArgumentNullException(nameof(playerBody));
            if (runtime.Combat == null || worldUnitsPerTile <= 0f || rangeUnits <= 0 || secondRangeUnits < rangeUnits || sightUnits < 0)
                throw new ArgumentException("보스 패턴·임시 거리 설정을 확인하세요.");
            runtime.ConfigureSpatialScale(worldUnitsPerTile);
            _rangeUnits = rangeUnits;
            _secondRangeUnits = secondRangeUnits;
            // 보스 전투는 이 드라이버가 방에 조립된 동안 거리와 화면에 관계없이 유지합니다.
            _combat = runtime.Combat;
            _phaseTwo = new BossPhaseTwoSelector(_combat);
            Action<bool> attackChanged = _scope.Guard<bool>(OnAttackWindowChanged);
            Action<bool> airborneChanged = _scope.Guard<bool>(OnAirborneChanged);
            Action<BossPattern> patternStarted = _scope.Guard<BossPattern>(_phaseTwo.Started);
            _scope.Add(() => _combat.AttackWindowChanged -= attackChanged);
            _scope.Add(() => _combat.AirborneChanged -= airborneChanged);
            _scope.Add(() => _combat.PatternStarted -= patternStarted);
            _combat.AttackWindowChanged += attackChanged;
            _combat.AirborneChanged += airborneChanged;
            _combat.PatternStarted += patternStarted;
        }

        public void Dispose()
        {
            if (_scope.IsDisposed) return;
            IsEngaged = false;
            SubscriptionScope.Cleanup(_scope.Dispose, _combat.Release);
        }

        // minimum·maximum은 보스 중심이 아니라 방 안쪽 면(정수 X)입니다. 몸 폭은 여기서 반영합니다.
        public void Step(float seconds, long minimum, long maximum)
        {
            if (_scope.IsDisposed || _runtime == null || _player == null || !_player.isActiveAndEnabled) return;
            BossCombat combat = _runtime.Combat;
            if (!_runtime.isActiveAndEnabled || _runtime.Actor.IsDead) { IsEngaged = false; return; }
            if (_runtime.Actor.IsExposed || _runtime.Actor.IsTransitioning || _runtime.Actor.CurrentState == HumanGhost.Data.Definitions.StateKey.Execute) return;
            if (_player.Actor.IsDead) { IsEngaged = false; combat.Release(); return; }
            long offset = checked(_player.Position.X - _runtime.Position.X);
            combat.Face(offset < 0 ? -1 : 1);
            // Unity 경계: 몸통 폭·발 높이는 읽는 즉시 정수로 바꿉니다.
            long extent = _runtime.ToInteger(_body.bounds.extents.x);
            _minX = checked(minimum + extent); _maxX = checked(maximum - extent);
            int milliseconds = _clock.AdvanceFrame(seconds);
            bool phaseTwo = _runtime.Actor.Phase.Phase > 0 && combat.HasPhaseTwoPatterns;
            _platformMilliseconds = checked(_runtime.ToInteger(_playerBody.bounds.min.y) - _runtime.ToInteger(_body.bounds.min.y)) >= _platformHeight
                ? Math.Min(_runtime.Actor.Phase.PlatformWaitTime + 1, _platformMilliseconds + milliseconds) : 0;
            if (AutomaticPatterns && phaseTwo && _phaseTwo.DesperationPending && combat.IsAttacking)
            { combat.Release(); combat.Face(offset < 0 ? -1 : 1); }
            int velocity = combat.Velocity;
            long distance = Math.Abs(offset);
            bool noticed = true;
            IsEngaged = noticed;
            if (AutomaticPatterns && phaseTwo && noticed && !combat.IsAttacking && !combat.IsWaiting)
            {
                int index = _phaseTwo.Choose(IntegerPosition.ToData(distance), _platformMilliseconds);
                if (index >= 0) combat.TryStart(index);
                velocity = combat.Velocity;
            }
            if (AutomaticPatterns && noticed && !combat.IsAttacking && !combat.IsWaiting && distance > IntegerPosition.FromData(_rangeUnits))
                velocity = combat.Facing * _runtime.Actor.Definition.MoveSpeed;
            bool wall = _runtime.MoveHorizontal(_motion.StepHorizontal(velocity, milliseconds), _minX, _maxX);
            if (wall) _motion.ResetHorizontal();
            if (wall) combat.NotifyWallHit();
            if (AutomaticPatterns && !phaseTwo && noticed && !combat.IsAttacking && !combat.IsWaiting) SelectPattern(distance);
            if (combat.IsAirborne)
            {
                // 코어 공중 판정(IsAirborne)과 같은 구간 동안 수평으로 옮깁니다. 첫 도약은 선딜 끝 LeapAirTime, 두 번째 도약은 그 타격의 선딜 전체입니다.
                // 공중이면 start ≤ HitElapsed < Windup이라 구간 길이는 0보다 큽니다.
                int windup = combat.CurrentPattern.Hits[combat.HitIndex].Windup;
                int start = combat.HitIndex == 0 ? windup - combat.LeapAirTime : 0;
                int travel = windup - start;
                long elapsed = Math.Max(0, Math.Min(travel, combat.HitElapsed - start));
                long x = checked(_leapStartX + (_landingX - _leapStartX) * elapsed / travel);
                _runtime.SetPosition(new IntegerPosition(x, _runtime.Position.Y));
            }
            CheckHit();
            Pull(milliseconds);
        }

        // 근접 거리는 사슬 외 패턴을 순환하고, 그 밖에서는 사슬이 닿으면 사슬을 씁니다.
        private void SelectPattern(long distance)
        {
            BossCombat combat = _runtime.Combat;
            bool melee = distance <= IntegerPosition.FromData(_rangeUnits);
            for (int i = 0; i < combat.PatternCount; i++)
            {
                int index = _nextPattern++ % combat.PatternCount;
                BossPattern pattern = combat.GetPattern(index);
                if (pattern.Kind != BossPatternKind.Legacy) continue;
                if (melee ? pattern.IsChain : !pattern.IsChain || distance > IntegerPosition.FromData(pattern.Range)) continue;
                if (combat.TryStart(index)) return;
            }
        }

        private void Pull(int milliseconds)
        {
            int velocity = _runtime.Combat.PullVelocity;
            if (velocity == 0) { _pullMotion.ResetHorizontal(); return; }
            long x = _player.Position.X;
            long stop = checked(_runtime.Position.X + _runtime.Combat.Facing * IntegerPosition.UnitsPerTile);
            long distance = _pullMotion.StepHorizontal(Math.Abs(velocity), milliseconds);
            long next = x < stop ? Math.Min(stop, x + distance) : Math.Max(stop, x - distance);
            _player.SetPosition(new IntegerPosition(next, _player.Position.Y));

        }

        private void OnAirborneChanged(bool airborne)
        {
            if (_runtime == null || _player == null || !_runtime.isActiveAndEnabled || !_player.isActiveAndEnabled) return;
            if (_runtime.Combat.CurrentPattern == null) return;
            if (airborne) { _leapStartX = _runtime.Position.X; _landingX = Math.Max(_minX, Math.Min(_maxX, _player.Position.X)); }
            else _runtime.SetPosition(new IntegerPosition(_landingX, _runtime.Position.Y));
        }

        private void OnAttackWindowChanged(bool active) { if (active) CheckHit(); }

        private void CheckHit()
        {
            BossCombat combat = _runtime.Combat;
            if (_scope.IsDisposed || _runtime == null || _player == null || _body == null || _playerBody == null ||
                !_runtime.isActiveAndEnabled || !_player.isActiveAndEnabled || !combat.IsAttackActive) return;
            var pattern = combat.CurrentPattern;
            if (_preparedPattern != pattern || _preparedHit != combat.HitIndex)
            {
                _preparedPattern = pattern; _preparedHit = combat.HitIndex;
                if (pattern.Kind == BossPatternKind.TripleSlam)
                    _runtime.MoveHorizontal(combat.Facing * IntegerPosition.UnitsPerTile, _minX, _maxX);
            }
            Bounds own = _body.bounds, target = _playerBody.bounds;
            long offset = checked(_player.Position.X - _runtime.Position.X);
            int rangeUnits = combat.CurrentPattern.IsChain ? combat.CurrentPattern.Range : combat.HitIndex > 0 ? _secondRangeUnits : _rangeUnits;
            bool touching = combat.IsCharging ? own.Intersects(target) :
                target.min.y <= own.max.y && target.max.y >= own.min.y && offset * combat.Facing >= 0 && Math.Abs(offset) <= IntegerPosition.FromData(rangeUnits);
            if (pattern.Areas.Count > 0)
            {
                long origin = pattern.Kind == BossPatternKind.DesperationLeap ? _landingX : _runtime.Position.X;
                BossHitArea area = pattern.Areas[combat.HitIndex];
                // 충격파는 화면의 흙먼지 원과 같은 현재 반지름까지만 맞습니다(B06).
                int reach = BossHitGeometry.IsShockwave(pattern, combat.HitIndex)
                    ? BossHitGeometry.ShockwaveReach(pattern, combat.HitIndex, combat.HitElapsed) : area.Range;
                // Unity 경계: 그림 높이는 읽는 즉시 정수로 바꿔 tile_x100으로 비교합니다.
                long bottom = _runtime.ToInteger(own.min.y);
                touching = BossHitGeometry.Contains(area, reach,
                    IntegerPosition.ToData(checked(_player.Position.X - origin)),
                    IntegerPosition.ToData(checked(_runtime.ToInteger(target.min.y) - bottom)),
                    IntegerPosition.ToData(checked(_runtime.ToInteger(target.max.y) - bottom)), combat.Facing);
            }
            if (!touching) return;
            int direction = Math.Sign(-offset);
            HitOutcome outcome;
            HitResolving?.Invoke(true);
            try { outcome = combat.TryHitPlayer(1, _player.Actor, _runtime.HitRandom, direction); }
            finally { HitResolving?.Invoke(false); }
            if (outcome >= HitOutcome.Hit) PlayerHit?.Invoke();
            else if (outcome == HitOutcome.Dodged) PlayerDodged?.Invoke();
        }
    }
}
