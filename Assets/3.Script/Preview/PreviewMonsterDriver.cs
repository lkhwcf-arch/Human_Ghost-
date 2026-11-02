using System;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay.Adapters;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 일반몹 한 개체의 임시 관측·이동·벽·타격 연결입니다. 운영 물리/스폰 정책은 소유하지 않습니다.
    internal sealed class PreviewMonsterDriver : IDisposable
    {
        private readonly MonsterRuntime _runtime;
        private readonly MonsterActor _monster;
        private readonly PlayerRuntime _player;
        private readonly SpriteRenderer _playerBody;
        private readonly SpriteRenderer _attackWindow;
        private readonly SpriteRenderer _body;
        private readonly Color _color;
        private readonly int _targetId;
        private readonly int _playerTargetId;
        private readonly float _worldUnitsPerTile;

        // targetId: 이 몬스터의 장면 내 양수 개체 ID, playerTargetId: 플레이어의 개체 ID(데이터 ID와 별개).
        public PreviewMonsterDriver(MonsterRuntime runtime, PlayerRuntime player, SpriteRenderer playerBody, SpriteRenderer attackWindow,
            int targetId, int playerTargetId, float worldUnitsPerTile)
        {
            _runtime = runtime != null ? runtime : throw new ArgumentNullException(nameof(runtime));
            _player = player != null ? player : throw new ArgumentNullException(nameof(player));
            _playerBody = playerBody != null ? playerBody : throw new ArgumentNullException(nameof(playerBody));
            _attackWindow = attackWindow != null ? attackWindow : throw new ArgumentNullException(nameof(attackWindow));
            if (targetId <= 0 || playerTargetId <= 0 || targetId == playerTargetId) throw new ArgumentOutOfRangeException(nameof(targetId));
            _monster = runtime.Actor ?? throw new InvalidOperationException("일반몹 초기화가 필요합니다.");
            _targetId = targetId; _playerTargetId = playerTargetId; _worldUnitsPerTile = worldUnitsPerTile;
            _body = runtime.GetComponentInChildren<SpriteRenderer>();
            if (_body == null) throw new InvalidOperationException("일반몹 임시 사각형이 없습니다.");
            _color = _body.color;
            // 판정 창이 한 프레임 안에 열리고 닫혀도 열리는 순간 한 번은 접촉을 확인합니다.
            _monster.AttackWindowChanged += OnAttackWindowChanged;
        }

        public void Dispose() => _monster.AttackWindowChanged -= OnAttackWindowChanged;

        public void Step(float seconds, float stageHalfWidth)
        {
            if (!_runtime.isActiveAndEnabled) return;
            if (_monster.IsDead) { _body.color = Color.gray; return; }
            PlayerActor actor = _player.Actor;
            Vector3 position = _runtime.transform.position;
            position.x += _monster.Velocity / 100f * _worldUnitsPerTile * seconds;
            bool wall = Mathf.Abs(position.x) > stageHalfWidth;
            position.x = Mathf.Clamp(position.x, -stageHalfWidth, stageHalfWidth);
            _runtime.transform.position = position;
            if (wall) _monster.NotifyWallHit();

            float offset = _player.transform.position.x - position.x;
            if (actor.IsDead) _monster.ClearTarget();
            else _monster.Observe(_playerTargetId, Distance(offset), offset > 0f ? 1 : offset < 0f ? -1 : 0);

            CheckMonsterHit();
            _body.color = _monster.IsDead ? Color.gray : _monster.CurrentState == StateKey.Stun ? new Color(0.75f, 0.35f, 0.95f) :
                _monster.IsAttackActive ? new Color(1f, 0.3f, 0.3f) : _color;
        }

        // 플레이어 판정 창과 이 몬스터의 접촉. GameplayPreview가 창 열림 순간과 매 프레임 호출합니다. 중복은 창 등록이 막습니다.
        public void CheckPlayerHit()
        {
            PlayerActor actor = _player.Actor;
            if (!_runtime.isActiveAndEnabled || _monster.IsDead || !actor.IsAttackActive) return;
            if (_attackWindow.bounds.Intersects(_body.bounds)) actor.TryHitMonster(_targetId, _monster, _player.HitRandom);
        }

        private void OnAttackWindowChanged(bool active)
        {
            if (active) CheckMonsterHit();
        }

        // 임시 판정: 근접·잡기는 높이가 겹치고 앞쪽 거리 안, 돌진은 사각형 겹침. 회피/치명은 확률 경로를 씁니다.
        private void CheckMonsterHit()
        {
            PlayerActor actor = _player.Actor;
            if (!_runtime.isActiveAndEnabled || !_monster.IsAttackActive || actor.IsDead) return;
            MonsterPattern pattern = _monster.CurrentPattern;
            float offset = _player.transform.position.x - _runtime.transform.position.x;
            Bounds own = _body.bounds, target = _playerBody.bounds;
            bool sameHeight = target.min.y <= own.max.y && target.max.y >= own.min.y;
            bool inFront = offset * _monster.Facing >= 0f;
            bool touching = pattern.IsCharge ? own.Intersects(target) : sameHeight && inFront && Distance(offset) <= pattern.Range;
            if (touching) _monster.TryHitPlayer(_playerTargetId, actor, _runtime.HitRandom);
        }

        private int Distance(float offset) => Mathf.RoundToInt(Mathf.Abs(offset) / _worldUnitsPerTile * 100f);
    }
}
