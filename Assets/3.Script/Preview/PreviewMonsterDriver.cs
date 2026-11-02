using System;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay.Adapters;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 일반몹 한 개체의 임시 관측·이동·벽·타격 연결입니다. 운영 물리/스폰 정책은 소유하지 않습니다.
    internal sealed class PreviewMonsterDriver
    {
        private readonly MonsterRuntime _runtime;
        private readonly PlayerRuntime _player;
        private readonly SpriteRenderer _playerBody;
        private readonly SpriteRenderer _attackWindow;
        private readonly SpriteRenderer _body;
        private readonly Color _color;
        private readonly int _targetId;

        public PreviewMonsterDriver(MonsterRuntime runtime, PlayerRuntime player, SpriteRenderer playerBody, SpriteRenderer attackWindow, int targetId)
        {
            _runtime = runtime != null ? runtime : throw new ArgumentNullException(nameof(runtime));
            _player = player != null ? player : throw new ArgumentNullException(nameof(player));
            _playerBody = playerBody != null ? playerBody : throw new ArgumentNullException(nameof(playerBody));
            _attackWindow = attackWindow != null ? attackWindow : throw new ArgumentNullException(nameof(attackWindow));
            if (targetId <= 0) throw new ArgumentOutOfRangeException(nameof(targetId));
            _targetId = targetId;
            _body = runtime.GetComponentInChildren<SpriteRenderer>();
            if (_body == null) throw new InvalidOperationException("일반몹 임시 사각형이 없습니다.");
            _color = _body.color;
        }

        public void Step(float seconds, float worldUnitsPerTile, float stageHalfWidth)
        {
            MonsterActor monster = _runtime.Actor;
            if (monster == null || !_runtime.isActiveAndEnabled) return;
            if (monster.IsDead) { _body.color = Color.gray; return; }
            PlayerActor actor = _player.Actor;
            Vector3 position = _runtime.transform.position;
            position.x += monster.Velocity / 100f * worldUnitsPerTile * seconds;
            bool wall = Mathf.Abs(position.x) > stageHalfWidth;
            position.x = Mathf.Clamp(position.x, -stageHalfWidth, stageHalfWidth);
            _runtime.transform.position = position;
            if (wall) monster.NotifyWallHit();

            float offset = _player.transform.position.x - position.x;
            int distance = Mathf.RoundToInt(Mathf.Abs(offset) / worldUnitsPerTile * 100f);
            if (actor.IsDead) monster.ClearTarget();
            else monster.Observe(actor.Definition.ID, distance, offset > 0f ? 1 : offset < 0f ? -1 : 0);

            if (monster.IsAttackActive)
            {
                MonsterPattern pattern = monster.CurrentPattern;
                Bounds own = _body.bounds, target = _playerBody.bounds;
                bool sameHeight = target.min.y <= own.max.y && target.max.y >= own.min.y;
                bool inFront = offset * monster.Facing >= 0f;
                bool touching = pattern.IsCharge ? own.Intersects(target) : sameHeight && inFront && distance <= pattern.Range;
                if (touching) monster.TryHitPlayer(actor.Definition.ID, actor, _runtime.HitRandom);
            }
            if (actor.IsAttackActive && _attackWindow.bounds.Intersects(_body.bounds))
                actor.TryHitMonster(_targetId, monster, _player.HitRandom);
            _body.color = monster.IsDead ? Color.gray : monster.CurrentState == StateKey.Stun ? new Color(0.75f, 0.35f, 0.95f) :
                monster.IsAttackActive ? new Color(1f, 0.3f, 0.3f) : _color;
        }
    }
}
