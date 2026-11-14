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
        private readonly float _worldUnitsPerTile;
        private readonly float _range;
        private readonly float _secondRange;
        private readonly float _sight;
        private int _nextPattern;
        private readonly BossPhaseTwoSelector _phaseTwo;
        private int _platformMilliseconds, _preparedHit = -1;
        private BossPattern _preparedPattern;
        private float _minX, _maxX, _landingX, _leapStartX;
        public float LandingX => _landingX;
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
        private const bool AutomaticPatterns = true;
#endif

        public PreviewBossDriver(BossRuntime runtime, PlayerRuntime player, SpriteRenderer body,
            SpriteRenderer playerBody, float worldUnitsPerTile, float range, float secondRange, float sight)
        {
            _runtime = runtime != null ? runtime : throw new ArgumentNullException(nameof(runtime));
            _player = player != null ? player : throw new ArgumentNullException(nameof(player));
            _body = body != null ? body : throw new ArgumentNullException(nameof(body));
            _playerBody = playerBody != null ? playerBody : throw new ArgumentNullException(nameof(playerBody));
            if (runtime.Combat == null || worldUnitsPerTile <= 0f || range <= 0f || secondRange < range)
                throw new ArgumentException("보스 패턴·임시 거리 설정을 확인하세요.");
            runtime.ConfigureSpatialScale(worldUnitsPerTile);
            _worldUnitsPerTile = worldUnitsPerTile;
            _range = range;
            _secondRange = secondRange;
            _sight = sight;
            runtime.Combat.AttackWindowChanged += OnAttackWindowChanged;
            runtime.Combat.AirborneChanged += OnAirborneChanged;
            _phaseTwo = new BossPhaseTwoSelector(runtime.Combat);
            runtime.Combat.PatternStarted += _phaseTwo.Started;
        }

        public void Dispose()
        {
            _runtime.Combat.AttackWindowChanged -= OnAttackWindowChanged;
            _runtime.Combat.AirborneChanged -= OnAirborneChanged;
            _runtime.Combat.PatternStarted -= _phaseTwo.Started;
            _runtime.Combat.Release();
        }

        public void Step(float seconds, float minX, float maxX)
        {
            BossCombat combat = _runtime.Combat;
            if (!_runtime.isActiveAndEnabled || _runtime.Actor.IsDead || _runtime.Actor.IsExposed || _runtime.Actor.IsTransitioning || _runtime.Actor.CurrentState == HumanGhost.Data.Definitions.StateKey.Execute) return;
            if (_player.Actor.IsDead) { combat.Release(); return; }
            float offset = _player.transform.position.x - _runtime.transform.position.x;
            combat.Face(offset < 0f ? -1 : 1);
            _minX = minX + _body.bounds.extents.x; _maxX = maxX - _body.bounds.extents.x;
            int milliseconds = _clock.Advance(seconds);
            bool phaseTwo = _runtime.Actor.Phase.Phase > 0 && combat.HasPhaseTwoPatterns;
            _platformMilliseconds = _playerBody.bounds.min.y - _body.bounds.min.y >= 1.5f * _worldUnitsPerTile
                ? Math.Min(3001, _platformMilliseconds + milliseconds) : 0;
            if (AutomaticPatterns && phaseTwo && _phaseTwo.DesperationPending && combat.IsAttacking)
            { combat.Release(); combat.Face(offset < 0f ? -1 : 1); }
            int velocity = combat.Velocity;
            bool noticed = _sight <= 0f || Mathf.Abs(offset) <= _sight * _worldUnitsPerTile;
            IsEngaged = noticed;
            if (AutomaticPatterns && phaseTwo && noticed && !combat.IsAttacking && !combat.IsWaiting)
            {
                int index = _phaseTwo.Choose(Mathf.RoundToInt(Mathf.Abs(offset) / _worldUnitsPerTile * 100f), _platformMilliseconds);
                if (index >= 0) combat.TryStart(index);
                velocity = combat.Velocity;
            }
            if (AutomaticPatterns && noticed && !combat.IsAttacking && !combat.IsWaiting && Mathf.Abs(offset) > _range * _worldUnitsPerTile)
                velocity = combat.Facing * _runtime.Actor.Definition.MoveSpeed;
            float extent = _body.bounds.extents.x;
            bool wall = _runtime.MoveHorizontal(_motion.StepHorizontal(velocity, milliseconds), minX + extent, maxX - extent);
            if (wall) _motion.ResetHorizontal();
            if (wall) combat.NotifyWallHit();
            if (AutomaticPatterns && !phaseTwo && noticed && !combat.IsAttacking && !combat.IsWaiting) SelectPattern(Mathf.Abs(offset));
            if (combat.IsAirborne)
            {
                float t = Mathf.Clamp01((combat.HitElapsed - (combat.HitIndex == 0 ? combat.CurrentPattern.Hits[0].Windup - 900 : 0)) / 900f);
                var position = _runtime.transform.position; position.x = Mathf.Lerp(_leapStartX, _landingX, t); _runtime.SetPosition(position);
            }
            CheckHit();
            Pull(milliseconds);
        }

        // 근접 거리는 사슬 외 패턴을 순환하고, 그 밖에서는 사슬이 닿으면 사슬을 씁니다.
        private void SelectPattern(float distance)
        {
            BossCombat combat = _runtime.Combat;
            bool melee = distance <= _range * _worldUnitsPerTile;
            for (int i = 0; i < combat.PatternCount; i++)
            {
                int index = _nextPattern++ % combat.PatternCount;
                BossPattern pattern = combat.GetPattern(index);
                if (pattern.Kind != BossPatternKind.Legacy) continue;
                if (melee ? pattern.IsChain : !pattern.IsChain || distance > pattern.Range / 100f * _worldUnitsPerTile) continue;
                if (combat.TryStart(index)) return;
            }
        }

        private void Pull(int milliseconds)
        {
            int velocity = _runtime.Combat.PullVelocity;
            if (velocity == 0) { _pullMotion.ResetHorizontal(); return; }
            long x = _player.Position.X;
            long stop = _player.ToInteger(_runtime.transform.position.x + _runtime.Combat.Facing * _worldUnitsPerTile);
            long distance = _pullMotion.StepHorizontal(Math.Abs(velocity), milliseconds);
            long next = x < stop ? Math.Min(stop, x + distance) : Math.Max(stop, x - distance);
            _player.SetPosition(new IntegerPosition(next, _player.Position.Y));

        }

        private void OnAirborneChanged(bool airborne)
        {
            if (_runtime.Combat.CurrentPattern == null) return;
            if (airborne) { _leapStartX = _runtime.transform.position.x; _landingX = Mathf.Clamp(_player.transform.position.x, _minX, _maxX); _runtime.LandingX = _landingX; }
            else { var position = _runtime.transform.position; position.x = _landingX; _runtime.SetPosition(position); }
        }

        private void OnAttackWindowChanged(bool active) { if (active) CheckHit(); }

        private void CheckHit()
        {
            BossCombat combat = _runtime.Combat;
            if (!_runtime.isActiveAndEnabled || !_player.isActiveAndEnabled || !combat.IsAttackActive) return;
            var pattern = combat.CurrentPattern;
            if (_preparedPattern != pattern || _preparedHit != combat.HitIndex)
            {
                _preparedPattern = pattern; _preparedHit = combat.HitIndex;
                if (pattern.Kind == BossPatternKind.TripleSlam)
                    _runtime.MoveHorizontal(combat.Facing * IntegerPosition.UnitsPerTile, _minX, _maxX);
            }
            Bounds own = _body.bounds, target = _playerBody.bounds;
            float offset = _player.transform.position.x - _runtime.transform.position.x;
            float range = combat.CurrentPattern.IsChain ? combat.CurrentPattern.Range / 100f : combat.HitIndex > 0 ? _secondRange : _range;
            bool touching = combat.IsCharging ? own.Intersects(target) :
                target.min.y <= own.max.y && target.max.y >= own.min.y && offset * combat.Facing >= 0f && Mathf.Abs(offset) <= range * _worldUnitsPerTile;
            if (pattern.Areas.Count > 0)
            {
                float origin = pattern.Kind == BossPatternKind.DesperationLeap ? _landingX : _runtime.transform.position.x;
                touching = BossHitGeometry.Contains(pattern.Areas[combat.HitIndex],
                    Mathf.RoundToInt((_player.transform.position.x - origin) / _worldUnitsPerTile * 100f),
                    Mathf.RoundToInt((target.min.y - own.min.y) / _worldUnitsPerTile * 100f),
                    Mathf.RoundToInt((target.max.y - own.min.y) / _worldUnitsPerTile * 100f), combat.Facing);
            }
            if (!touching) return;
            int direction = System.Math.Sign(_runtime.transform.position.x - _player.transform.position.x);
            HitOutcome outcome;
            HitResolving?.Invoke(true);
            try { outcome = combat.TryHitPlayer(1, _player.Actor, _runtime.HitRandom, direction); }
            finally { HitResolving?.Invoke(false); }
            if (outcome >= HitOutcome.Hit) PlayerHit?.Invoke();
            else if (outcome == HitOutcome.Dodged) PlayerDodged?.Invoke();
        }
    }
}
