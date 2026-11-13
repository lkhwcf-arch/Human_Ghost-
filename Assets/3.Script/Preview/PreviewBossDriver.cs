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
        private MillisecondClock _clock;
        private readonly IntegerMotion _motion = new IntegerMotion();
        private readonly IntegerMotion _pullMotion = new IntegerMotion();
        public event Action PlayerHit;
        public event Action PlayerDodged;
        // 추적 중이거나 패턴을 쓰는 동안입니다. 전투 BGM·HUD에 씁니다.
        public bool IsEngaged { get; private set; }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public bool AutomaticPatterns { get; set; } = true;
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
        }

        public void Dispose()
        {
            _runtime.Combat.AttackWindowChanged -= OnAttackWindowChanged;
            _runtime.Combat.Release();
        }

        public void Step(float seconds, float minX, float maxX)
        {
            BossCombat combat = _runtime.Combat;
            if (!_runtime.isActiveAndEnabled || _runtime.Actor.IsDead || _runtime.Actor.IsExposed || _runtime.Actor.IsInvulnerable) return;
            if (_player.Actor.IsDead) { combat.Release(); return; }
            float offset = _player.transform.position.x - _runtime.transform.position.x;
            if (!combat.IsAttacking) combat.Face(offset < 0f ? -1 : 1);
            int milliseconds = _clock.Advance(seconds);
            int velocity = combat.Velocity;
            bool noticed = _sight <= 0f || Mathf.Abs(offset) <= _sight * _worldUnitsPerTile;
            bool automatic = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            automatic = AutomaticPatterns;
#endif
            IsEngaged = noticed;
            if (automatic && noticed && !combat.IsAttacking && !combat.IsWaiting && Mathf.Abs(offset) > _range * _worldUnitsPerTile)
                velocity = combat.Facing * _runtime.Actor.Definition.MoveSpeed;
            float extent = _body.bounds.extents.x;
            bool wall = _runtime.MoveHorizontal(_motion.StepHorizontal(velocity, milliseconds), minX + extent, maxX - extent);
            if (wall) _motion.ResetHorizontal();
            if (wall) combat.NotifyWallHit();
            if (automatic && noticed && !combat.IsAttacking && !combat.IsWaiting) SelectPattern(Mathf.Abs(offset));
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
                if (melee ? pattern.IsChain : !pattern.IsChain || distance > pattern.Range / 100f * _worldUnitsPerTile) continue;
                if (combat.TryStart(index)) return;
            }
        }

        private void Pull(int milliseconds)
        {
            int velocity = _runtime.Combat.PullVelocity;
            if (velocity == 0) { _pullMotion.ResetHorizontal(); return; }
            long x = _player.Position.X;
            long stop = _player.ToInteger(_runtime.transform.position.x + _runtime.Combat.Facing * _range * _worldUnitsPerTile * 0.5f);
            long distance = _pullMotion.StepHorizontal(Math.Abs(velocity), milliseconds);
            long next = x < stop ? Math.Min(stop, x + distance) : Math.Max(stop, x - distance);
            _player.SetPosition(new IntegerPosition(next, _player.Position.Y));

        }

        private void OnAttackWindowChanged(bool active) { if (active) CheckHit(); }

        private void CheckHit()
        {
            BossCombat combat = _runtime.Combat;
            if (!_runtime.isActiveAndEnabled || !_player.isActiveAndEnabled || !combat.IsAttackActive) return;
            Bounds own = _body.bounds, target = _playerBody.bounds;
            float offset = _player.transform.position.x - _runtime.transform.position.x;
            float range = combat.CurrentPattern.IsChain ? combat.CurrentPattern.Range / 100f : combat.HitIndex > 0 ? _secondRange : _range;
            bool touching = combat.IsCharging ? own.Intersects(target) :
                target.min.y <= own.max.y && target.max.y >= own.min.y && offset * combat.Facing >= 0f && Mathf.Abs(offset) <= range * _worldUnitsPerTile;
            if (!touching) return;
            int direction = System.Math.Sign(_runtime.transform.position.x - _player.transform.position.x);
            HitOutcome outcome = combat.TryHitPlayer(1, _player.Actor, _runtime.HitRandom, direction);
            if (outcome >= HitOutcome.Hit) PlayerHit?.Invoke();
            else if (outcome == HitOutcome.Dodged) PlayerDodged?.Invoke();
        }
    }
}
