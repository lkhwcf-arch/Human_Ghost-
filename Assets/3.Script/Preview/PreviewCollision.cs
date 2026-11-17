using System;
using HumanGhost.Gameplay.Adapters;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 플레이어 상자로 맵 Collider2D를 조회합니다. 위치를 옮기지 않고 이동 가능한 거리만 답합니다.
    // 맵이 없는 장면은 시작 높이를 바닥으로 씁니다.
    // [정수화 미적용 이유] Unity Physics2D 어댑터입니다. 조회의 시작점·방향·거리와 결과 접촉점·법선은 Unity API가 float만 받고
    // 돌려주므로 실수로 남습니다. 호출한 이동 소유자가 결과 거리를 정수 위치로 바꿔 확정합니다.
    // 기본 동작은 플레이어(비탈 개선 보류)가 쓰며, 일반 몬스터는 아래 PreviewFootCollision이 벽 판정 두 지점을 재정의합니다.
    internal class PreviewCollision
    {
        public const float Skin = 0.02f;
        private readonly bool _useColliders;
        private readonly float _flatFloorY;
        private readonly Vector2 _halfSize;
        private readonly Transform _dropThroughRoot;
        private readonly RaycastHit2D[] _hits = new RaycastHit2D[16];
        private ContactFilter2D _filter;
        // 파생 조회가 같은 형상·필터·버퍼를 재사용합니다(조회마다 할당하지 않음).
        protected bool UsesColliders => _useColliders;
        protected Vector2 HalfSize => _halfSize;
        protected ContactFilter2D Filter => _filter;
        protected RaycastHit2D[] Hits => _hits;

        public PreviewCollision(bool useColliders, float flatFloorY, Vector2 halfSize, Transform dropThroughRoot = null)
        {
            _useColliders = useColliders; _flatFloorY = flatFloorY; _halfSize = halfSize; _dropThroughRoot = dropThroughRoot;
            _filter = new ContactFilter2D { useTriggers = false, useLayerMask = true, layerMask = Physics2D.DefaultRaycastLayers };
            Physics2D.SyncTransforms();
        }

        public float MoveHorizontal(Vector3 position, float delta)
        {
            if (!_useColliders || delta == 0f) return delta;
            float direction = Mathf.Sign(delta);
            // 발밑 이음매에 걸리지 않게 높이를 조금 줄여 조회합니다.
            var size = new Vector2(_halfSize.x * 2f, _halfSize.y * 2f - Skin * 4f);
            int count = Physics2D.BoxCast(position, size, 0f, new Vector2(direction, 0f), _filter, _hits, Mathf.Abs(delta) + Skin);
            float allowed = Mathf.Abs(delta);
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = _hits[i];
                if (hit.collider.usedByEffector || hit.normal.x * direction > -0.5f || IgnoresWallHit(hit)) continue;
                allowed = Mathf.Min(allowed, Mathf.Max(0f, hit.distance - Skin));
            }
            return direction * LimitHorizontal(position, direction, allowed);
        }

        // 재정의 지점: 기본은 모든 벽 접촉을 막습니다.
        protected virtual bool IgnoresWallHit(RaycastHit2D hit) => false;
        // 재정의 지점: 벽 판정 뒤 허용 거리를 추가로 제한합니다. 기본은 그대로 둡니다.
        protected virtual float LimitHorizontal(Vector3 position, float direction, float allowed) => allowed;

        // 천장에 막히면 hitCeiling이 true입니다. 단방향 발판은 아래에서 통과합니다.
        public float MoveUp(Vector3 position, float delta, out bool hitCeiling)
        {
            hitCeiling = false;
            if (!_useColliders) return delta;
            var size = new Vector2(_halfSize.x * 2f - Skin * 4f, _halfSize.y * 2f);
            int count = Physics2D.BoxCast(position, size, 0f, Vector2.up, _filter, _hits, delta + Skin);
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = _hits[i];
                if (hit.collider.usedByEffector || hit.normal.y > -0.5f) continue;
                hitCeiling = true;
                return Mathf.Max(0f, hit.distance - Skin);
            }
            return delta;
        }

        private Collider2D _ignoredPlatform;
        public bool TryBeginDrop(Vector3 position)
        {
            if (!_useColliders) return false;
            var size = new Vector2(_halfSize.x * 2f - Skin * 4f, _halfSize.y * 2f);
            int count = Physics2D.BoxCast(position, size, 0f, Vector2.down, _filter, _hits, 0.08f);
            Collider2D support = null;
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var hit = _hits[i];
                if (hit.normal.y < 0.5f || hit.distance >= nearest) continue;
                support = hit.collider;
                nearest = hit.distance;
            }
            if (!CanDropThrough(support)) return false;
            _ignoredPlatform = support;
            return true;
        }

        private bool CanDropThrough(Collider2D support)
        {
            if (support == null || !support.usedByEffector) return false;
            DropThroughPlatform policy = support.GetComponentInParent<DropThroughPlatform>();
            // 명시 정책이 우선이며, 호출자가 허용한 맵 범위의 단방향 발판만 기본 허용합니다.
            return policy != null ? policy.Allowed : _dropThroughRoot != null && support.transform.IsChildOf(_dropThroughRoot);
        }

        // 몸통 폭 비탈 추종입니다. 플레이어가 쓰며 2026-11-17 변경에서 동작을 바꾸지 않았습니다.
        // [정수 경사 규칙 미적용 이유] 플레이어 비탈 개선은 사용자 결정으로 보류입니다. 적용 시 PreviewFootCollision을 재사용합니다.
        public bool TryFollowGroundSlope(Vector3 position, float delta, out float height)
        {
            height = position.y;
            if (!_useColliders || delta == 0f ||
                !FindFloor(position, 0.05f, out _, out Collider2D currentFloor)) return false;

            // 단차나 다른 층으로 건너뛰지 않도록 이번 수평 이동 거리만큼의 경사만 따라갑니다.
            float reach = Mathf.Abs(delta) + Skin * 2f;
            Vector3 target = position + new Vector3(delta, reach, 0f);
            if (!FindFloor(target, reach * 2f, out float gap, out Collider2D nextFloor) ||
                nextFloor.usedByEffector ||
                (!IsGroundSlope(currentFloor) && !IsGroundSlope(nextFloor))) return false;

            float nextHeight = target.y - gap;
            float rise = Mathf.Max(0f, nextHeight - position.y);
            if (rise > 0f && MoveUp(position, rise, out _) < rise - Skin) return false;
            position.y += rise;
            if (Mathf.Abs(MoveHorizontal(position, delta) - delta) > 0.0001f) return false;

            height = nextHeight;
            return true;
        }

        // 경사 이동 대상 지면입니다. 몬스터 보행 구간 탐색도 같은 기준을 사용합니다.
        internal static bool IsGroundSlope(Collider2D collider) =>
            collider is PolygonCollider2D && collider.gameObject.name.StartsWith("GroundSlope_", System.StringComparison.Ordinal);

        public bool FindFloor(Vector3 position, float distance, out float floorDistance) =>
            FindFloor(position, distance, out floorDistance, out _);

        private bool FindFloor(Vector3 position, float distance, out float floorDistance, out Collider2D floor)
        {
            floorDistance = distance;
            floor = null;
            if (!_useColliders)
            {
                float gap = position.y - _flatFloorY;
                if (gap > distance) return false;
                floorDistance = Mathf.Max(0f, gap);
                return true;
            }
            bool found = false;
            float feet = position.y - _halfSize.y;
            if (_ignoredPlatform != null && position.y + _halfSize.y < _ignoredPlatform.bounds.min.y - Skin)
                _ignoredPlatform = null;
            var size = new Vector2(_halfSize.x * 2f - Skin * 4f, _halfSize.y * 2f);
            int count = Physics2D.BoxCast(position, size, 0f, Vector2.down, _filter, _hits, distance + Skin);
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = _hits[i];
                if (hit.normal.y < 0.5f) continue;
                // 이음새에서 같은 높이의 다른 조각에 다시 착지하지 않습니다. 아래층·금지 지면은 유지합니다.
                if (_ignoredPlatform != null && (hit.collider == _ignoredPlatform ||
                    (Mathf.Abs(hit.collider.bounds.max.y - _ignoredPlatform.bounds.max.y) <= Skin && CanDropThrough(hit.collider)))) continue;
                // 단방향 발판은 발이 윗면보다 위에 있을 때만 밟습니다.
                if (hit.collider.usedByEffector && feet < hit.collider.bounds.max.y - Skin) continue;
                float value = Mathf.Max(0f, hit.distance - Skin);
                if (!found || value < floorDistance)
                {
                    floorDistance = value;
                    floor = hit.collider;
                }
                found = true;
            }
            return found;
        }
    }

    // 중앙 발 지지 비탈 조회입니다(2026-11-17, 일반 몬스터). 비탈 높이를 몸통 폭이 아닌 몸통 중앙의 발로 잽니다.
    // PreviewCollision의 벽 판정 두 지점을 재정의하고 정수 이동량의 비탈 추종을 더합니다. 형상·필터·버퍼·바닥 조회는 기반을 재사용합니다.
    // 이동량·높이 변화는 IntegerPosition 정수로 주고받고 경사 판정은 코어 GroundSlopeRule(분모 10000)이 합니다.
    // [정수화 미적용 이유] 내부의 float는 Physics2D 조회 입력과 접촉점·법선 결과뿐이며, 판정 전에 정수로 바꿉니다.
    internal sealed class PreviewFootCollision : PreviewCollision
    {
        // 정수 위치와 Physics2D 월드 좌표 사이 변환 배율입니다(SpatialRuntime과 같은 값).
        private readonly float _worldUnitsPerTile;

        public PreviewFootCollision(Vector2 halfSize, float worldUnitsPerTile) : base(true, 0f, halfSize)
        {
            if (worldUnitsPerTile <= 0f || float.IsNaN(worldUnitsPerTile) || float.IsInfinity(worldUnitsPerTile))
                throw new ArgumentOutOfRangeException(nameof(worldUnitsPerTile));
            _worldUnitsPerTile = worldUnitsPerTile;
        }

        // Unity 물리 경계 변환입니다. 기존 SpatialRuntime 변환을 재사용합니다.
        private long ToInteger(float world) => SpatialRuntime.ToInteger(world, _worldUnitsPerTile);
        private float ToWorld(long units) => SpatialRuntime.ToWorld(units, _worldUnitsPerTile);
        // Physics2D 법선(float 단위 벡터)을 분모 10000 정수로 바꿔 코어 경사 규칙으로 판정합니다.
        private static bool IsWalkable(Vector2 normal) => GroundSlopeRule.IsWalkableNormal(
            Mathf.RoundToInt(normal.x * GroundSlopeRule.RatioScale), Mathf.RoundToInt(normal.y * GroundSlopeRule.RatioScale));

        // 몸통 모서리는 중앙 발보다 먼저 비탈에 닿거나 비탈 속에 들어갑니다. 최대 경사 이내의 비탈 면과
        // 시작부터 겹친 비탈은 벽이 아니며, 높이는 중앙 발이 닿을 때 TryFollowFootSlope가 맞춥니다.
        protected override bool IgnoresWallHit(RaycastHit2D hit)
            => IsGroundSlope(hit.collider) && (hit.distance <= 0f || IsWalkable(hit.normal));

        // 비탈 추종이 실패한 수평 이동이 중앙 발 아래 더 높은 지면(오르막) 속으로 들어가지 않게 합니다.
        protected override float LimitHorizontal(Vector3 position, float direction, float allowed)
        {
            if (allowed <= 0f) return allowed;
            float feet = position.y - HalfSize.y;
            return FindFootSurface(position.x + direction * allowed, position.y, HalfSize.y + Skin, out float surface, out _) &&
                ToInteger(surface) - ToInteger(feet) > GroundSlopeRule.ObservationTolerance ? 0f : allowed;
        }

        // center는 몸통 중심(Unity 경계 관측), run은 수평 정수 이동량이며 성공하면 발 높이 변화 rise를 정수로 돌려줍니다.
        // 위치 확정은 호출한 이동 소유자가 합니다.
        // 1) 현재·다음 발 중앙 지면을 관측하고 2) 높이 변화를 정수로 바꿔 GroundSlopeRule로 판정하며
        // 3) 비탈이 몸통 폭 안에서 차지할 수 있는 높이만큼 아래를 올린 상자로 벽·천장 경로를 검사합니다.
        public bool TryFollowFootSlope(Vector3 center, long run, out long rise)
        {
            rise = 0;
            if (!UsesColliders || run == 0) return false;
            Vector2 half = HalfSize;
            float feet = center.y - half.y;
            float probe = ToWorld(GroundSlopeRule.ObservationTolerance) + Skin;
            if (!FindFootSurface(center.x, feet + probe, probe * 2f, out _, out Collider2D currentFloor)) return false;
            // 조회 범위는 정수 규칙의 최대 높이 변화에서 만듭니다. 이동량에 비례하므로 프레임률과 무관합니다.
            float window = ToWorld(GroundSlopeRule.MaxRise(run));
            float delta = ToWorld(run);
            if (!FindFootSurface(center.x + delta, feet + window, window * 2f, out float surface, out Collider2D nextFloor) ||
                nextFloor.usedByEffector || (!IsGroundSlope(currentFloor) && !IsGroundSlope(nextFloor))) return false;
            long observed = checked(ToInteger(surface) - ToInteger(feet));
            if (!GroundSlopeRule.CanTraverse(run, observed)) return false;

            // 몸통 반폭 × 최대 경사: 몸통 폭 안에서 비탈이 차지할 수 있는 최대 높이입니다. 이 띠 위만 벽·천장으로 검사합니다.
            float band = half.x * GroundSlopeRule.MaxSlopeRatio / GroundSlopeRule.RatioScale + probe;
            float bodyHeight = half.y * 2f - band;
            if (bodyHeight > Skin * 2f)
            {
                var size = new Vector2(half.x * 2f, bodyHeight - Skin);
                var start = new Vector2(center.x, feet + band + bodyHeight * .5f);
                var move = new Vector2(delta, ToWorld(observed));
                RaycastHit2D[] hits = Hits;
                int count = Physics2D.BoxCast(start, size, 0f, move.normalized, Filter, hits, move.magnitude + Skin);
                for (int i = 0; i < count; i++)
                    if (!hits[i].collider.usedByEffector) return false;
            }
            rise = observed;
            return true;
        }

        // x 위치의 발 중앙 지면을 위에서 아래로 찾습니다. 시작점이 이미 지면 안이면 최대 경사를 넘는 면으로 보고 실패합니다.
        private bool FindFootSurface(float x, float top, float distance, out float surface, out Collider2D floor)
        {
            surface = top; floor = null;
            RaycastHit2D[] hits = Hits;
            int count = Physics2D.Raycast(new Vector2(x, top), Vector2.down, Filter, hits, distance);
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = hits[i];
                if (hit.distance <= 0f) return false;
                if (!IsWalkable(hit.normal) || hit.distance >= nearest) continue;
                nearest = hit.distance; surface = hit.point.y; floor = hit.collider;
            }
            return floor != null;
        }
    }
}
