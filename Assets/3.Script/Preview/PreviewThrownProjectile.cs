using UnityEngine;
using HumanGhost.Gameplay.Adapters;

namespace HumanGhost.Gameplay.Preview
{
    // 짐 투척(C08) 표시입니다. 던진 순간의 목표로 판정 창 동안 포물선을 그립니다. 피해 판정은 몬스터 코어가 맡습니다.
    internal sealed class PreviewThrownProjectile
    {
        private long _arcUnits = IntegerPosition.UnitsPerTile * 3 / 2;
        private readonly SpriteRenderer _view;
        // [정수화 미적용 이유] 정수 좌표(IntegerPosition)와 Unity 월드 좌표를 오가는 변환 계수(Inspector 타일당 화면 거리)입니다. 좌표 상태는 정수입니다.
        private readonly float _worldUnitsPerTile;
        private readonly Vector3 _hitSize;
        private IntegerPosition _start;
        // [정수화 미적용 이유] Transform z(그리기 순서) 표시값입니다.
        private float _z;
        private IntegerPosition _end;
        private int _elapsed;
        private MillisecondClock _clock;
        private int _duration;

        public PreviewThrownProjectile(SpriteRenderer view, float worldUnitsPerTile)
        {
            _view = view; _worldUnitsPerTile = worldUnitsPerTile;
            _hitSize = view.bounds.size;
        }

        public bool IsFlying => _view.enabled;
        public int ElapsedMilliseconds => _elapsed;
        public int DurationMilliseconds => _duration;
        // 0~1 비행 진행률입니다.
        // [정수화 미적용 이유] 정수 ms 경과에서 계산한 0~1 표시 진행률입니다(그림자 크기 보간).
        public float Progress => _duration <= 0 ? 0f : _elapsed / (float)_duration;
        public Bounds Bounds => new Bounds(_view.transform.position, _hitSize);

        // arcTiles: 출발·도착 직선 위로 솟는 높이(칸). 보통 던지기 1.5, 높이 던지기는 더 높게 씁니다.
        public void Throw(Vector3 from, Vector3 to, int milliseconds, int arcTiles100 = 150)
        {
            if (milliseconds <= 0) throw new System.ArgumentOutOfRangeException(nameof(milliseconds));
            if (arcTiles100 < 0) throw new System.ArgumentOutOfRangeException(nameof(arcTiles100));
            _arcUnits = IntegerPosition.UnitsPerTile * arcTiles100 / 100;
            _start = FromWorld(from); _end = FromWorld(to); _duration = milliseconds; _elapsed = 0; _clock = default;
            _z = from.z;
            _view.transform.position = from;
            _view.enabled = true;
        }

        public void Step(float seconds)
        {
            if (!_view.enabled) return;
            _elapsed += System.Math.Min(_clock.AdvanceFrame(seconds), _duration - _elapsed);
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
