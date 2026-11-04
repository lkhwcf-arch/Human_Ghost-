using System;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 호출자가 전달한 표식 묶음의 매달리기 모서리(K04)입니다. 표식 Hang_*을 플레이어가 있는 쪽에서 잡습니다.
    internal sealed class PreviewHangEdges
    {
        private const float _grabReach = 0.45f;
        private const float _grabHeight = 0.7f;
        private readonly Transform[] _markers;
        private readonly Vector2 _halfSize;

        public PreviewHangEdges(Transform[] markers, Vector2 halfSize)
        {
            _markers = markers ?? Array.Empty<Transform>();
            _halfSize = halfSize;
        }

        // side는 플레이어에서 모서리로 향하는 방향입니다(왼쪽에서 잡으면 1).
        public bool TryFind(Vector3 position, out Transform edge, out int side)
        {
            foreach (Transform marker in _markers)
            {
                if (marker == null || !marker.name.StartsWith("Hang_", StringComparison.Ordinal)) continue;
                Vector3 point = marker.position;
                int markerSide = position.x <= point.x ? 1 : -1;
                float front = position.x + markerSide * _halfSize.x;
                if (Mathf.Abs(front - point.x) > _grabReach || Mathf.Abs(position.y + _halfSize.y - point.y) > _grabHeight) continue;
                edge = marker; side = markerSide;
                return true;
            }
            edge = null; side = 0;
            return false;
        }

        public Vector3 HangPosition(Transform edge, int side, float z)
        {
            Vector3 point = edge.position;
            return new Vector3(point.x - side * _halfSize.x, point.y - _halfSize.y, z);
        }

        public Vector3 ClimbPosition(Transform edge, int side, float z)
        {
            Vector3 point = edge.position;
            return new Vector3(point.x + side * (_halfSize.x + PreviewCollision.Skin), point.y + _halfSize.y + PreviewCollision.Skin, z);
        }
    }
}
