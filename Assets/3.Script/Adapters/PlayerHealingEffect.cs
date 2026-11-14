using UnityEngine;
using HumanGhost.Gameplay.Presentation;
namespace HumanGhost.Gameplay.Adapters
{
    // 실제 약초·처형 회복 성공만 소비합니다. 중단·최대 HP·저장 복원에는 재생하지 않습니다.
    public sealed class PlayerHealingEffect : MonoBehaviour
    {
        [SerializeField] private PlayerRuntime _player = null;
        [SerializeField] private SpriteRenderer _body = null;
        [SerializeField] private SpriteClipEffect _effect = null;
        private PlayerActor _actor;
        private void OnEnable() { _player.Initialized += Bind; Bind(_player.Actor); }
        private void Bind(PlayerActor actor)
        {
            if (_actor != null) { _actor.HerbRecovered -= Show; _actor.ExecutionRecovered -= Show; }
            _actor = actor;
            if (_actor != null) { _actor.HerbRecovered += Show; _actor.ExecutionRecovered += Show; }
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
