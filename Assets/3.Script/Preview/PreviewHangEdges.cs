using System;
using HumanGhost.Gameplay.Adapters;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 호출자가 전달한 표식 묶음의 매달리기 모서리(K04)입니다. 표식 Hang_*을 플레이어가 있는 쪽에서 잡습니다.
    internal sealed class PreviewHangEdges
    {
        // 잡기 판정 범위입니다(tile_x100: 앞쪽 0.45칸·높이 0.7칸). 2026-11-18 float 월드 값에서 정수로 옮겼습니다(타일당 화면 거리 1 기준 동일).
        private const int _grabReachUnits = 45;
        private const int _grabHeightUnits = 70;
        private readonly Transform[] _markers;
        private readonly SpatialRuntime _space;
        // [정수화 미적용 이유] 플레이어 충돌 상자 크기(Inspector Vector2)입니다. 플레이어 이동은 float Physics2D 계약(비탈 보류, 2026-11-17)을 유지하므로
        // 원본은 실수로 받고, 잡기 판정에서는 정수로 바꿔 비교합니다. 매달림·오르기 위치 출력도 이 값과 표식 Transform으로 만듭니다.
        private readonly Vector2 _halfSize;

        public PreviewHangEdges(Transform[] markers, Vector2 halfSize, SpatialRuntime space)
        {
            _markers = markers ?? Array.Empty<Transform>();
            _halfSize = halfSize;
            _space = space != null ? space : throw new ArgumentNullException(nameof(space));
        }

        // side는 플레이어에서 모서리로 향하는 방향입니다(왼쪽에서 잡으면 1).
        public bool TryFind(Vector3 position, out Transform edge, out int side, Transform excluded = null)
        {
            foreach (Transform marker in _markers)
            {
                if (marker == null || marker == excluded || !marker.gameObject.activeInHierarchy ||
                    !marker.name.StartsWith("Hang_", StringComparison.Ordinal)) continue;
                if (!IsInReach(position, marker, out side)) continue;
                edge = marker;
                return true;
            }
            edge = null; side = 0;
            return false;
        }

        public bool IsInReach(Vector3 position, Transform edge) => IsInReach(position, edge, out _);

        // Unity 경계: 플레이어·표식 Transform 위치는 읽는 즉시 정수로 바꾸고 판정은 정수로 합니다.
        private bool IsInReach(Vector3 position, Transform edge, out int side)
        {
            side = 0;
            if (edge == null) return false;
            IntegerPosition player = _space.FromWorld(position), point = _space.FromWorld(edge.position);
            side = player.X <= point.X ? 1 : -1;
            long front = checked(player.X + side * _space.ToInteger(_halfSize.x));
            long feetToEdge = checked(player.Y + _space.ToInteger(_halfSize.y) - point.Y);
            return Math.Abs(checked(front - point.X)) <= IntegerPosition.FromData(_grabReachUnits) &&
                Math.Abs(feetToEdge) <= IntegerPosition.FromData(_grabHeightUnits);
        }

        // 매달림·오르기 위치는 표식 Transform에서 만든 월드 좌표로 돌려주고, 호출자가 SetPosition·FromWorld로 즉시 정수로 바꿉니다.
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
