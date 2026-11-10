using UnityEngine;
using HumanGhost.Gameplay.Adapters;

namespace HumanGhost.Gameplay.Preview
{
    // 짐 투척(C08) 표시입니다. 던진 순간의 목표로 판정 창 동안 포물선을 그립니다. 피해 판정은 몬스터 코어가 맡습니다.
    internal sealed class PreviewThrownProjectile
    {
        private const long _arcUnits = IntegerPosition.UnitsPerTile * 3 / 2;
        private readonly SpriteRenderer _view;
        private readonly float _worldUnitsPerTile;
        private IntegerPosition _start;
        private float _z;
        private IntegerPosition _end;
        private int _elapsed;
        private MillisecondClock _clock;
        private int _duration;

        public PreviewThrownProjectile(SpriteRenderer view, float worldUnitsPerTile)
        {
            _view = view; _worldUnitsPerTile = worldUnitsPerTile;
        }

        public bool IsFlying => _view.enabled;
        public Bounds Bounds => _view.bounds;

        public void Throw(Vector3 from, Vector3 to, int milliseconds)
        {
            if (milliseconds <= 0) throw new System.ArgumentOutOfRangeException(nameof(milliseconds));
            _start = FromWorld(from); _end = FromWorld(to); _duration = milliseconds; _elapsed = 0; _clock = default;
            _z = from.z;
            _view.transform.position = from;
            _view.enabled = true;
        }

        public void Step(float seconds)
        {
            if (!_view.enabled) return;
            _elapsed += System.Math.Min(_clock.Advance(seconds), _duration - _elapsed);
            long x = _start.X + (_end.X - _start.X) * _elapsed / _duration;
            long y = _start.Y + (_end.Y - _start.Y) * _elapsed / _duration;
            y += 4 * _arcUnits * _elapsed * (_duration - _elapsed) / ((long)_duration * _duration);
            _view.transform.position = new Vector3(ToWorld(x), ToWorld(y), _z);
        }

        private IntegerPosition FromWorld(Vector3 position) => new IntegerPosition(
            SpatialRuntime.ToInteger(position.x, _worldUnitsPerTile),
            SpatialRuntime.ToInteger(position.y, _worldUnitsPerTile));
        private float ToWorld(long value) => SpatialRuntime.ToWorld(value, _worldUnitsPerTile);

        public void Stop() => _view.enabled = false;
        // 씬을 내릴 때는 표시가 먼저 사라졌을 수 있습니다.
        public void Destroy() { if (_view != null) Object.Destroy(_view.gameObject); }
    }
}
