using System;
using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    // 캐릭터의 공통 위치 소유자입니다. Transform은 입력·충돌 경계와 화면 출력에만 사용합니다.
    public abstract class SpatialRuntime : MonoBehaviour
    {
        private IntegerPosition _position;
        private bool _hasPosition;
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
        public bool MoveHorizontal(long delta, float minX, float maxX)
        {
            IntegerPosition position = Position;
            long requested = checked(position.X + delta);
            long minimum = ToInteger(minX), maximum = ToInteger(maxX);
            if (minimum > maximum) throw new ArgumentException("이동 구간의 최소값이 최대값보다 큽니다.");
            long x = Math.Max(minimum, Math.Min(maximum, requested));
            SetPosition(new IntegerPosition(x, position.Y));
            return requested < minimum || requested > maximum;
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
