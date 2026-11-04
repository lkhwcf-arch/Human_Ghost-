using System;
using System.Collections.Generic;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 구덩이 낙하 구역(L08)입니다. 표식 묶음의 FallZone_이름(BoxCollider2D)과 FallRespawn_이름을 짝짓습니다.
    internal sealed class PreviewFallZones
    {
        private struct Zone { public Bounds Area; public Vector3 Respawn; }
        private readonly Zone[] _zones;

        // 짝이 없으면 조립 실패로 전달합니다.
        public PreviewFallZones(Transform[] markers)
        {
            var zones = new List<Zone>();
            foreach (Transform marker in markers ?? Array.Empty<Transform>())
            {
                if (marker == null || !marker.name.StartsWith("FallZone_", StringComparison.Ordinal)) continue;
                var area = marker.GetComponent<BoxCollider2D>();
                if (area == null) throw new InvalidOperationException(marker.name + "에 BoxCollider2D가 필요합니다.");
                string respawnName = "FallRespawn_" + marker.name.Substring("FallZone_".Length);
                Transform respawn = null;
                foreach (Transform other in markers) if (other != null && other.name == respawnName) respawn = other;
                if (respawn == null) throw new InvalidOperationException(respawnName + " 지점이 없습니다.");
                // 물리 반영 시점에 의존하지 않게 Transform으로 영역을 계산합니다.
                Vector3 center = marker.TransformPoint(area.offset);
                Vector3 scale = marker.lossyScale;
                var size = new Vector3(area.size.x * Mathf.Abs(scale.x), area.size.y * Mathf.Abs(scale.y), 1f);
                zones.Add(new Zone { Area = new Bounds(center, size), Respawn = respawn.position });
            }
            _zones = zones.ToArray();
        }

        // 프레임이 길어도 지나치지 않게 이번 프레임 낙하 구간 전체와 비교합니다.
        public bool TryHit(float x, float previousFeet, float feet, out Vector3 respawn)
        {
            foreach (Zone zone in _zones)
            {
                if (x < zone.Area.min.x || x > zone.Area.max.x ||
                    Mathf.Max(feet, previousFeet) < zone.Area.min.y || Mathf.Min(feet, previousFeet) > zone.Area.max.y) continue;
                respawn = zone.Respawn;
                return true;
            }
            respawn = default;
            return false;
        }
    }
}
