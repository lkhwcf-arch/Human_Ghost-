using System;
using HumanGhost.Gameplay.Adapters;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 테스트씬 전용 순환 선택·이동·접촉. 운영 AI·물리·사슬 패턴은 담당하지 않습니다.
    internal sealed class PreviewBossDriver : IDisposable
    {
        private readonly BossRuntime _runtime;
        private readonly PlayerRuntime _player;
        private readonly SpriteRenderer _body;
        private readonly SpriteRenderer _playerBody;
        private readonly float _worldUnitsPerTile;
        private readonly float _range;
        private readonly float _secondRange;
        private int _nextPattern;

        public PreviewBossDriver(BossRuntime runtime, PlayerRuntime player, SpriteRenderer body,
            SpriteRenderer playerBody, float worldUnitsPerTile, float range, float secondRange)
        {
            _runtime = runtime != null ? runtime : throw new ArgumentNullException(nameof(runtime));
            _player = player != null ? player : throw new ArgumentNullException(nameof(player));
            _body = body != null ? body : throw new ArgumentNullException(nameof(body));
            _playerBody = playerBody != null ? playerBody : throw new ArgumentNullException(nameof(playerBody));
            if (runtime.Combat == null || worldUnitsPerTile <= 0f || range <= 0f || secondRange < range)
                throw new ArgumentException("보스 패턴·임시 거리 설정을 확인하세요.");
            _worldUnitsPerTile = worldUnitsPerTile;
            _range = range;
            _secondRange = secondRange;
            runtime.Combat.AttackWindowChanged += OnAttackWindowChanged;
        }

        public void Dispose()
        {
            _runtime.Combat.AttackWindowChanged -= OnAttackWindowChanged;
            _runtime.Combat.Release();
        }

        public void Step(float seconds, float stageHalfWidth)
        {
            BossCombat combat = _runtime.Combat;
            if (!_runtime.isActiveAndEnabled || _runtime.Actor.IsDead || _runtime.Actor.IsExposed || _runtime.Actor.IsInvulnerable) return;
            if (_player.Actor.IsDead) { combat.Release(); return; }
            float offset = _player.transform.position.x - _runtime.transform.position.x;
            if (!combat.IsAttacking) combat.Face(offset < 0f ? -1 : 1);
            Vector3 position = _runtime.transform.position;
            float velocity = combat.Velocity;
            if (!combat.IsAttacking && !combat.IsWaiting && Mathf.Abs(offset) > _range * _worldUnitsPerTile)
                velocity = combat.Facing * _runtime.Actor.Definition.MoveSpeed;
            position.x += velocity / 100f * _worldUnitsPerTile * seconds;
            float limit = Mathf.Max(0f, stageHalfWidth - _body.bounds.extents.x);
            bool wall = Mathf.Abs(position.x) >= limit;
            position.x = Mathf.Clamp(position.x, -limit, limit);
            _runtime.transform.position = position;
            if (wall) combat.NotifyWallHit();
            if (!combat.IsAttacking && !combat.IsWaiting && Mathf.Abs(offset) <= _range * _worldUnitsPerTile)
                for (int i = 0; i < combat.PatternCount; i++)
                {
                    int index = _nextPattern++ % combat.PatternCount;
                    if (combat.TryStart(index)) break;
                }
            CheckHit();
        }

        private void OnAttackWindowChanged(bool active) { if (active) CheckHit(); }

        private void CheckHit()
        {
            BossCombat combat = _runtime.Combat;
            if (!_runtime.isActiveAndEnabled || !_player.isActiveAndEnabled || !combat.IsAttackActive) return;
            Bounds own = _body.bounds, target = _playerBody.bounds;
            float offset = _player.transform.position.x - _runtime.transform.position.x;
            float range = combat.HitIndex > 0 ? _secondRange : _range;
            bool touching = combat.IsCharging ? own.Intersects(target) :
                target.min.y <= own.max.y && target.max.y >= own.min.y && offset * combat.Facing >= 0f && Mathf.Abs(offset) <= range * _worldUnitsPerTile;
            if (touching) combat.TryHitPlayer(1, _player.Actor, _runtime.HitRandom);
        }
    }
}
