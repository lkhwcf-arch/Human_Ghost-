using HumanGhost.Gameplay.Adapters;
using HumanGhost.Gameplay.Presentation;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 패링 표시의 객체·수명을 독립적으로 소유합니다. 코어 상태를 변경하지 않습니다.
    internal sealed class PreviewParryFeedback : System.IDisposable
    {
        private readonly GameSpeedController _speed;
        private readonly PreviewFollowCamera _camera;
        private readonly GameAudio _audio;
        private readonly SpriteClipEffect _prefab;
        private SpriteClipEffect _impact;
        public PreviewParryFeedback(GameSpeedController speed, PreviewFollowCamera camera, GameAudio audio, SpriteClipEffect prefab)
        { _speed = speed; _camera = camera; _audio = audio; _prefab = prefab; }
        public void Show(int hitStop, Vector3 position)
        {
            try
            {
                _speed?.State.HitStop(hitStop);
                _camera?.Shake(0.08f, 0.1f);
                _audio?.PlayEffect("SFX_PARRY");
                if (_prefab == null) return;
                if (_impact == null) _impact = Object.Instantiate(_prefab);
                _impact.Play(position);
            }
            catch (System.Exception error) { Debug.LogException(error); Dispose(); }
        }
        public void Step(float unscaledSeconds) { if (_impact != null) _impact.Step(unscaledSeconds); }
        public void Dispose() { if (_impact != null) Object.Destroy(_impact.gameObject); }
    }
}
