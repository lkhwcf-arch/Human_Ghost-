using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 불붙은 짚(C08) 표시와 접촉입니다. 주기·지속은 코어 GroundFire가 맡습니다.
    internal sealed class PreviewGroundFireView
    {
        private readonly GroundFire _fire;
        private readonly SpriteRenderer _view;

        public PreviewGroundFireView(GroundFire fire, SpriteRenderer view)
        {
            _fire = fire; _view = view;
        }

        public void Step(float seconds, PlayerActor player, Bounds playerBounds)
        {
            if (!_fire.IsActive) return;
            int ticks = _fire.Tick(Mathf.RoundToInt(seconds * 1000f));
            if (ticks > 0 && !player.IsDead && _view.bounds.Intersects(playerBounds)) player.ReceiveHit(_fire.Damage);
            if (!_fire.IsActive) _view.enabled = false;
        }

        public void Destroy() => Object.Destroy(_view.gameObject);
    }
}
