using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 짐 투척(C08) 표시입니다. 던진 순간의 목표로 판정 창 동안 포물선을 그립니다. 피해 판정은 몬스터 코어가 맡습니다.
    internal sealed class PreviewThrownProjectile
    {
        private const float _arcTiles = 1.5f;
        private readonly SpriteRenderer _view;
        private readonly float _worldUnitsPerTile;
        private Vector3 _start;
        private Vector3 _end;
        private float _elapsed;
        private float _seconds;

        public PreviewThrownProjectile(SpriteRenderer view, float worldUnitsPerTile)
        {
            _view = view; _worldUnitsPerTile = worldUnitsPerTile;
        }

        public bool IsFlying => _view.enabled;
        public Bounds Bounds => _view.bounds;

        public void Throw(Vector3 from, Vector3 to, float seconds)
        {
            _start = from; _end = to; _seconds = seconds; _elapsed = 0f;
            _view.transform.position = from;
            _view.enabled = true;
        }

        public void Step(float seconds)
        {
            if (!_view.enabled) return;
            _elapsed += seconds;
            float progress = Mathf.Clamp01(_elapsed / _seconds);
            Vector3 position = Vector3.Lerp(_start, _end, progress);
            position.y += 4f * _arcTiles * _worldUnitsPerTile * progress * (1f - progress);
            _view.transform.position = position;
        }

        public void Stop() => _view.enabled = false;
        public void Destroy() => Object.Destroy(_view.gameObject);
    }
}
