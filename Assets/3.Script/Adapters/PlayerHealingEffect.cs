using UnityEngine;
using HumanGhost.Gameplay.Presentation;
namespace HumanGhost.Gameplay.Adapters
{
    // 약초 회복 성공 알림만 소비합니다. 중단된 섭취·다른 회복·데이터 복원에는 재생하지 않습니다.
    public sealed class PlayerHealingEffect : MonoBehaviour
    {
        [SerializeField] private PlayerRuntime _player = null;
        [SerializeField] private SpriteRenderer _body = null;
        [SerializeField] private SpriteClipEffect _effect = null;
        private PlayerActor _actor;
        private void OnEnable() { _player.Initialized += Bind; Bind(_player.Actor); }
        private void Bind(PlayerActor actor)
        {
            if (_actor != null) _actor.HerbRecovered -= Show;
            _actor = actor;
            if (_actor != null) _actor.HerbRecovered += Show;
        }
        private void Show()
        {
            try { _effect.Play(new Vector3(_body.bounds.center.x, _body.bounds.min.y, _body.transform.position.z - .1f)); }
            catch (System.Exception error) { enabled = false; Debug.LogException(error, this); }
        }
        private void LateUpdate()
        {
            if (!_effect.IsPlaying) return;
            _effect.transform.position = new Vector3(_body.bounds.center.x, _body.bounds.min.y, _body.transform.position.z - .1f);
            _effect.Step(Time.deltaTime);
        }
        private void OnDisable()
        {
            _player.Initialized -= Bind; Bind(null);
            _effect.gameObject.SetActive(false);
        }
    }
}
