using System;
using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    // 캐릭터의 공통 위치 소유자입니다. Transform은 입력·충돌 경계와 화면 출력에만 사용합니다.
    public abstract class SpatialRuntime : MonoBehaviour
    {
        private IntegerPosition _position;
        private bool _hasPosition;
        // [정수화 미적용 이유] 정수 좌표(IntegerPosition)와 Unity 월드 좌표를 오가는 변환 계수(Inspector 타일당 화면 거리)입니다. 좌표 상태는 정수입니다.
        private float _worldUnitsPerTile = 1f;
        public event Action SpatialChanged;
        public event Action<bool> AvailabilityChanged;
        protected void NotifyAvailability(bool available) => AvailabilityChanged?.Invoke(available);
        public IntegerPosition Position { get { EnsurePosition(); return _position; } }
        public void ConfigureSpatialScale(float worldUnitsPerTile)
        {
            if (worldUnitsPerTile <= 0f || float.IsNaN(worldUnitsPerTile) || float.IsInfinity(worldUnitsPerTile))
                throw new ArgumentOutOfRangeException(nameof(worldUnitsPerTile));
            if (_hasPosition && _worldUnitsPerTile == worldUnitsPerTile) return;
            _worldUnitsPerTile = worldUnitsPerTile;
            _position = FromWorld(transform.position); _hasPosition = true;
        }
        public static long ToInteger(float world, float worldUnitsPerTile) => checked((long)Math.Round((double)world / worldUnitsPerTile * IntegerPosition.UnitsPerTile, MidpointRounding.ToEven));
        public static float ToWorld(long units, float worldUnitsPerTile) => (float)((double)units / IntegerPosition.UnitsPerTile * worldUnitsPerTile);
        public long ToInteger(float world) => ToInteger(world, _worldUnitsPerTile);
        public float ToWorld(long units) => ToWorld(units, _worldUnitsPerTile);
        public IntegerPosition FromWorld(Vector3 position) => new IntegerPosition(ToInteger(position.x), ToInteger(position.y));
        public Vector3 ToWorld(IntegerPosition position) => new Vector3(ToWorld(position.X), ToWorld(position.Y), transform.position.z);
        public void SetPosition(Vector3 position) => SetPosition(FromWorld(position), position.z);
        public void SetPosition(IntegerPosition position) => SetPosition(position, transform.position.z);
        public bool MoveHorizontal(long delta, float minX, float maxX) => MoveHorizontal(delta, ToInteger(minX), ToInteger(maxX));
        // 정수 구간 버전입니다. 구간을 정수로 가진 소유자는 실수 변환 없이 씁니다.
        public bool MoveHorizontal(long delta, long minimum, long maximum)
        {
            long x = ClampHorizontal(delta, minimum, maximum, out bool hitEdge);
            SetPosition(new IntegerPosition(x, Position.Y));
            return hitEdge;
        }
        // 위치를 바꾸지 않고 구간 제한을 적용한 수평 목표 X만 계산합니다(이벤트 없음).
        // 높이까지 정한 뒤 SetPosition으로 한 번에 확정해 구독자가 중간 위치를 관측하지 않게 할 때 씁니다.
        public long ClampHorizontal(long delta, float minX, float maxX, out bool hitEdge)
            => ClampHorizontal(delta, ToInteger(minX), ToInteger(maxX), out hitEdge);
        // 정수 구간 버전입니다. 구간을 정수로 가진 소유자는 실수 변환 없이 씁니다.
        public long ClampHorizontal(long delta, long minimum, long maximum, out bool hitEdge)
        {
            long requested = checked(Position.X + delta);
            if (minimum > maximum) throw new ArgumentException("이동 구간의 최소값이 최대값보다 큽니다.");
            hitEdge = requested < minimum || requested > maximum;
            return Math.Max(minimum, Math.Min(maximum, requested));
        }
        private void SetPosition(IntegerPosition position, float z)
        {
            EnsurePosition();
            bool changed = !_position.Equals(position) || transform.position.z != z;
            if (!changed) return;
            _position = position;
            transform.position = new Vector3(ToWorld(position.X), ToWorld(position.Y), z);
            SpatialChanged?.Invoke();
        }
        // 외부 물리·부모 이동을 수행한 소유자가 완료 후 명시적으로 동기화합니다.
        public void NotifySpatialChanged()
        {
            _position = FromWorld(transform.position); _hasPosition = true;
            SpatialChanged?.Invoke();
        }
        protected void NotifySpatialStateChanged() => SpatialChanged?.Invoke();
        private void EnsurePosition()
        {
            if (_hasPosition) return;
            _position = FromWorld(transform.position); _hasPosition = true;
        }
    }
}
