using HumanGhost.Gameplay.Adapters;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 패링 표시의 객체·수명을 독립적으로 소유합니다. 코어 상태를 변경하지 않습니다.
    internal sealed class PreviewParryFeedback : System.IDisposable
    {
        private readonly GameSpeedController _speed;
        private readonly PreviewFollowCamera _camera;
        private readonly GameAudio _audio;
        public PreviewParryFeedback(GameSpeedController speed, PreviewFollowCamera camera, GameAudio audio)
        { _speed = speed; _camera = camera; _audio = audio; }
        private GameObject _parrySpark;
        private Sprite _sparkSprite;
        private float _sparkRemaining;

        public void Show(int hitStop, Vector3 position)
        {
            _speed?.State.HitStop(hitStop);
            _camera?.Shake(0.08f, 0.1f);
            _audio?.PlayEffect("SFX_PARRY");
            if (_parrySpark == null)
            {
                _parrySpark = new GameObject("ParrySpark");
                var texture = Texture2D.whiteTexture;
                _sparkSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * 0.5f, texture.width);
                for (int i = 0; i < 4; i++)
                {
                    var ray = new GameObject("Spark" + i);
                    ray.transform.SetParent(_parrySpark.transform, false);
                    ray.transform.localRotation = Quaternion.Euler(0f, 0f, i * 45f);
                    ray.transform.localScale = new Vector3(0.6f, 0.035f, 1f);
                    var view = ray.AddComponent<SpriteRenderer>(); view.sprite = _sparkSprite; view.sortingOrder = 100;
                }
            }
            _parrySpark.transform.position = position;
            _sparkRemaining = 0.12f; _parrySpark.SetActive(true);
        }

        public void Step(float unscaledSeconds)
        {
            if (_sparkRemaining > 0f && (_sparkRemaining -= unscaledSeconds) <= 0f && _parrySpark != null) _parrySpark.SetActive(false);
        }
        public void Dispose()
        {
            if (_parrySpark != null) Object.Destroy(_parrySpark);
            if (_sparkSprite != null) Object.Destroy(_sparkSprite);
        }
    }
}
