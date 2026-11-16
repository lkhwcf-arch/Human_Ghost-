using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 플레이어 상자로 맵 Collider2D를 조회합니다. 위치를 옮기지 않고 이동 가능한 거리만 답합니다.
    // 맵이 없는 장면은 시작 높이를 바닥으로 씁니다.
    internal sealed class PreviewCollision
    {
        public const float Skin = 0.02f;
        private readonly bool _useColliders;
        private readonly float _flatFloorY;
        private readonly Vector2 _halfSize;
        private readonly Transform _dropThroughRoot;
        private readonly RaycastHit2D[] _hits = new RaycastHit2D[16];
        private ContactFilter2D _filter;

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
                if (hit.collider.usedByEffector || hit.normal.x * direction > -0.5f) continue;
                allowed = Mathf.Min(allowed, Mathf.Max(0f, hit.distance - Skin));
            }
            return direction * allowed;
        }

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

        private static bool IsGroundSlope(Collider2D collider) =>
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
}
