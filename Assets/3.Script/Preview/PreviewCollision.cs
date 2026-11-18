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
        // [정수화 미적용 이유] Unity Physics2D 어댑터의 조회 여유·평지 높이·충돌 상자 크기입니다(클래스 주석 참고). 플레이어 충돌 상자·이동은 float Physics2D 계약(비탈 보류, 2026-11-17)을 유지하므로 이 경계에서 실수로 둡니다.
        public const float Skin = 0.02f;
        private readonly bool _useColliders;
        // [정수화 미적용 이유] Unity Physics2D 어댑터의 조회 여유·평지 높이·충돌 상자 크기입니다(클래스 주석 참고). 플레이어 충돌 상자·이동은 float Physics2D 계약(비탈 보류, 2026-11-17)을 유지하므로 이 경계에서 실수로 둡니다.
        private readonly float _flatFloorY;
        private readonly Vector2 _halfSize;
        private readonly Transform _dropThroughRoot;
        private readonly RaycastHit2D[] _hits = new RaycastHit2D[16];
        private ContactFilter2D _filter;
        // 파생 조회가 같은 형상·필터·버퍼를 재사용합니다(조회마다 할당하지 않음).
        protected bool UsesColliders => _useColliders;
        // [정수화 미적용 이유] Unity Physics2D 어댑터의 조회 여유·평지 높이·충돌 상자 크기입니다(클래스 주석 참고). 플레이어 충돌 상자·이동은 float Physics2D 계약(비탈 보류, 2026-11-17)을 유지하므로 이 경계에서 실수로 둡니다.
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
            GetWallBox(position, out Vector2 boxCenter, out Vector2 size);
            int count = Physics2D.BoxCast(boxCenter, size, 0f, new Vector2(direction, 0f), _filter, _hits, Mathf.Abs(delta) + Skin);
            float allowed = Mathf.Abs(delta);
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = _hits[i];
                if (hit.collider.usedByEffector || hit.normal.x * direction > -0.5f || IgnoresWallHit(hit)) continue;
                allowed = Mathf.Min(allowed, Mathf.Max(0f, hit.distance - Skin));
            }
            return direction * LimitHorizontal(position, direction, allowed);
        }

        // 재정의 지점: 수평 벽 검사 상자입니다. 기본은 몸통에서 발밑 이음매에 걸리지 않게 높이만 조금 줄인 상자입니다.
        protected virtual void GetWallBox(Vector3 position, out Vector2 center, out Vector2 size)
        {
            center = position;
            size = new Vector2(_halfSize.x * 2f, _halfSize.y * 2f - Skin * 4f);
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
    // PreviewCollision의 벽 판정 두 지점을 재정의하고 정수 이동량의 비탈 추종을 더합니다. 필터·버퍼·바닥 조회는 기반을 재사용합니다.
    // 계산은 모두 IntegerPosition 정수입니다. Unity 값(몸통 중심·크기, 기반 MoveHorizontal의 float 인자)은 들어오는 즉시 정수로 바꾸고
    // Physics2D Raycast·BoxCast를 호출하는 순간에만 월드 좌표로 바꿉니다. 경사 판정은 코어 GroundSlopeRule(분모 10000)입니다.
    // [정수화 미적용 이유] Physics2D API의 입력(시작점·방향·거리)과 결과(접촉점·법선·거리)는 float만 지원합니다.
    internal sealed class PreviewFootCollision : PreviewCollision
    {
        // 정수 위치와 Physics2D 월드 좌표 사이 변환 배율입니다(SpatialRuntime과 같은 값).
        // [정수화 미적용 이유] 정수 좌표(IntegerPosition)와 Unity 월드 좌표를 오가는 변환 계수(Inspector 타일당 화면 거리)입니다. 좌표 상태는 정수입니다.
        private readonly float _worldUnitsPerTile;
        private readonly long _halfWidth, _halfHeight, _skin;
        // float 유효숫자는 24비트라 좌표 크기 M의 관측 오차는 약 M × 2^-23입니다. 접촉점·발 높이 두 관측 여유로 8배(M >> 20)를
        // 정수로 계산해 GroundSlopeRule에 넘깁니다. 맵 좌표 수백 칸·144fps에서도 경사 한계를 약 0.3° 이내로만 넓힙니다.
        private const int _observationErrorShift = 20;

        public PreviewFootCollision(Vector2 halfSize, float worldUnitsPerTile) : base(true, 0f, halfSize)
        {
            if (worldUnitsPerTile <= 0f || float.IsNaN(worldUnitsPerTile) || float.IsInfinity(worldUnitsPerTile))
                throw new ArgumentOutOfRangeException(nameof(worldUnitsPerTile));
            _worldUnitsPerTile = worldUnitsPerTile;
            _halfWidth = ToInteger(halfSize.x); _halfHeight = ToInteger(halfSize.y); _skin = ToInteger(Skin);
        }

        // Unity 물리 경계 변환입니다. 기존 SpatialRuntime 변환을 재사용합니다.
        private long ToInteger(float world) => SpatialRuntime.ToInteger(world, _worldUnitsPerTile);
        private float ToWorld(long units) => SpatialRuntime.ToWorld(units, _worldUnitsPerTile);
        private static long ObservationError(long x, long y)
            => Math.Max(Math.Max(Math.Abs(x), Math.Abs(y)), IntegerPosition.UnitsPerTile) >> _observationErrorShift;
        // Physics2D 법선(float 단위 벡터)을 분모 10000 정수로 바꿔 코어 경사 규칙으로 판정합니다.
        private static bool IsWalkable(Vector2 normal) => GroundSlopeRule.IsWalkableNormal(
            Mathf.RoundToInt(normal.x * GroundSlopeRule.RatioScale), Mathf.RoundToInt(normal.y * GroundSlopeRule.RatioScale));

        // 몸통 모서리는 중앙 발보다 먼저 비탈에 닿거나 비탈 속에 들어갑니다. 최대 경사 이내의 비탈 면과
        // 시작부터 겹친(거리 0) 비탈은 벽이 아니며, 높이는 중앙 발이 닿을 때 TryFollowFootSlope가 맞춥니다.
        protected override bool IgnoresWallHit(RaycastHit2D hit)
            => IsGroundSlope(hit.collider) && (hit.distance <= 0f || IsWalkable(hit.normal));

        // 비탈 추종이 실패한 수평 이동이 중앙 발 아래 더 높은 지면(오르막) 속으로 들어가지 않게 합니다.
        // 현재·다음 지지면의 높이 차이로 판단합니다(몸통 아래변 기준 아님, 아래 FindSupport 참고). 지지면이 없으면 제한하지 않습니다.
        // 인자는 기반 MoveHorizontal의 float 계약이므로 들어오는 즉시 정수로 바꿔 비교합니다.
        protected override float LimitHorizontal(Vector3 position, float direction, float allowed)
        {
            if (allowed <= 0f) return allowed;
            long x = ToInteger(position.x), y = ToInteger(position.y);
            long moved = checked(ToInteger(allowed) * Math.Sign(direction));
            long error = ObservationError(x, y);
            if (!FindSupport(x, y, error, out long current, out _)) return allowed;
            // 다음 위치의 탐사 시작점(몸통 중심)이 이미 지면 속이면 높은 장애물이므로 막습니다. 지면이 없으면(낭떠러지) 기존 구간 정책에 맡깁니다.
            if (!FindSupport(checked(x + moved), y, error, out long next, out _, out bool startInside)) return startInside ? 0f : allowed;
            return checked(next - current) > GroundSlopeRule.ObservationTolerance + error ? 0f : allowed;
        }

        // 수평 벽 검사 상자의 아래쪽을 실제 지지면보다 낮게 내리지 않습니다.
        // 이유: 그림 보정으로 Body가 지면 아래까지 내려가면(30589c84) 상자가 지면 속 비탈 다각형의 수직 옆면에 닿아
        // 비탈 시작점에서 벽으로 막히고 방향을 바꾸며 떨렸습니다(11/17). 지지면이 없으면(공중) 기반 몸통 상자를 씁니다.
        protected override void GetWallBox(Vector3 position, out Vector2 center, out Vector2 size)
        {
            long x = ToInteger(position.x), y = ToInteger(position.y);
            if (!FindSupport(x, y, ObservationError(x, y), out long support, out _)) { base.GetWallBox(position, out center, out size); return; }
            long bottom = checked(Math.Max(y - _halfHeight, support) + _skin * 2), top = checked(y + _halfHeight - _skin * 2);
            if (top - bottom <= _skin) { base.GetWallBox(position, out center, out size); return; }
            // Physics2D BoxCast 호출 경계입니다. 정수로 정한 상자를 월드 좌표로만 바꿉니다.
            center = new Vector2(ToWorld(x), ToWorld(checked(bottom + (top - bottom) / 2)));
            size = new Vector2(ToWorld(_halfWidth * 2), ToWorld(top - bottom));
        }

        // 정수 수평 이동 판정입니다. 이동 소유자는 이 함수로 정수만 주고받습니다.
        // [정수화 미적용 이유] 기반 MoveHorizontal은 플레이어(보류)와 공유하는 float Physics2D 계약이라 여기서만 변환합니다.
        public long MoveHorizontal(long centerX, long centerY, long run)
        {
            if (run == 0) return 0;
            float allowed = MoveHorizontal(new Vector3(ToWorld(centerX), ToWorld(centerY), 0f), ToWorld(run));
            return checked(ToInteger(Mathf.Abs(allowed)) * Math.Sign(run));
        }

        // 몸통 중심 아래의 실제 지지면을 찾습니다. 발 기준을 몸통(Body) 아래변으로 두지 않습니다.
        // 이유: 프리팹은 그림을 지면에 붙이려고 Body를 지면보다 낮게 둘 수 있습니다(11/16 30589c84, 허수귀 0.2칸).
        // 이동 판정이 그림 보정 값에 의존하지 않도록 관측한 지지면끼리의 높이 차이만 씁니다.
        private bool FindSupport(long x, long y, long error, out long surface, out Collider2D floor)
            => FindSupport(x, y, error, out surface, out floor, out _);

        private bool FindSupport(long x, long y, long error, out long surface, out Collider2D floor, out bool startInside)
            => FindFootSurface(x, y, checked(_halfHeight + _skin + GroundSlopeRule.ObservationTolerance + error), out surface, out floor, out startInside);

        // (x, y)는 몸통 중심의 정수 좌표, run은 수평 정수 이동량이며 성공하면 발 높이 변화 rise를 정수로 돌려줍니다.
        // 위치 확정은 호출한 이동 소유자가 합니다.
        // 1) 현재·다음 발 중앙 지면을 관측하고 2) 높이 변화를 GroundSlopeRule로 판정하며
        // 3) 비탈이 몸통 폭 안에서 차지할 수 있는 높이만큼 아래를 올린 상자로 벽·천장 경로를 검사합니다.
        public bool TryFollowFootSlope(long x, long y, long run, out long rise)
        {
            rise = 0;
            if (!UsesColliders || run == 0) return false;
            long error = ObservationError(x, y);
            long probe = checked(GroundSlopeRule.ObservationTolerance + error + _skin);
            // 현재 발 = 몸통 중심 아래의 실제 지지면입니다(Body 그림 보정과 무관, FindSupport 참고).
            if (!FindSupport(x, y, error, out long current, out Collider2D currentFloor)) return false;
            // 조회 범위는 정수 규칙의 최대 높이 변화(관측 오차 포함)입니다. 이동량에 비례하므로 프레임률과 무관합니다.
            long window = GroundSlopeRule.MaxRise(run, error);
            if (!FindFootSurface(checked(x + run), checked(current + window), checked(window * 2), out long surface, out Collider2D nextFloor) ||
                nextFloor.usedByEffector || (!IsGroundSlope(currentFloor) && !IsGroundSlope(nextFloor))) return false;
            long observed = checked(surface - current);
            if (!GroundSlopeRule.CanTraverse(run, observed, error)) return false;

            // 몸통 반폭 × 최대 경사: 몸통 폭 안에서 비탈이 차지할 수 있는 최대 높이입니다. 지지면에서 이 띠 위부터 몸통 윗변까지만
            // 벽·천장으로 검사합니다(몸통 윗변은 Body 기준, 아래 기준은 실제 지지면).
            long band = checked(_halfWidth * GroundSlopeRule.MaxSlopeRatio / GroundSlopeRule.RatioScale + probe);
            long bodyHeight = checked(y + _halfHeight - (current + band));
            if (bodyHeight > _skin * 2)
            {
                // 경로 길이는 정수 제곱근으로 구합니다. Physics2D BoxCast 호출 줄에서만 월드 좌표로 바꿉니다(방향은 Unity가 정규화).
                long length = checked(IntegerDistance.Sqrt(checked(run * run + observed * observed)) + _skin);
                RaycastHit2D[] hits = Hits;
                int count = Physics2D.BoxCast(new Vector2(ToWorld(x), ToWorld(checked(current + band + bodyHeight / 2))),
                    new Vector2(ToWorld(_halfWidth * 2), ToWorld(bodyHeight - _skin)), 0f,
                    new Vector2(ToWorld(run), ToWorld(observed)), Filter, hits, ToWorld(length));
                for (int i = 0; i < count; i++)
                    if (!hits[i].collider.usedByEffector) return false;
            }
            rise = observed;
            return true;
        }

        // 정수 x 위치의 발 중앙 지면을 top에서 아래로 distance만큼 찾습니다. 시작점이 이미 지면 안이면 최대 경사를 넘는 면으로 보고 실패합니다.
        // Raycast 호출과 결과(접촉점·거리)만 월드 좌표이며 결과는 바로 정수로 바꿉니다.
        private bool FindFootSurface(long x, long top, long distance, out long surface, out Collider2D floor)
            => FindFootSurface(x, top, distance, out surface, out floor, out _);

        // startInside: 시작점이 이미 콜라이더 안이었는지입니다(높은 장애물·최대 경사 초과 면).
        private bool FindFootSurface(long x, long top, long distance, out long surface, out Collider2D floor, out bool startInside)
        {
            surface = top; floor = null; startInside = false;
            RaycastHit2D[] hits = Hits;
            int count = Physics2D.Raycast(new Vector2(ToWorld(x), ToWorld(top)), Vector2.down, Filter, hits, ToWorld(distance));
            long nearest = long.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = hits[i];
                if (hit.distance <= 0f) { startInside = true; return false; }
                long hitDistance = ToInteger(hit.distance);
                if (!IsWalkable(hit.normal) || hitDistance >= nearest) continue;
                nearest = hitDistance; surface = ToInteger(hit.point.y); floor = hit.collider;
            }
            return floor != null;
        }
    }
}
