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
        private SubscriptionScope _scope, _actorScope;
        private void OnEnable()
        {
            if (_player == null || _body == null || _effect == null)
            {
                Debug.LogError("회복 효과의 플레이어·몸체·표시 연결이 필요합니다.", this);
                enabled = false;
                return;
            }
            var player = _player;
            var scope = _scope = new SubscriptionScope();
            System.Action<PlayerActor> initialized = scope.Guard<PlayerActor>(Bind);
            scope.Add(() => player.Initialized -= initialized); player.Initialized += initialized;
            Bind(player.Actor);
        }
        private void Bind(PlayerActor actor)
        {
            _actorScope?.Dispose(); _actorScope = null;
            if (actor != null)
            {
                var scope = _actorScope = new SubscriptionScope();
                System.Action show = scope.Guard(Show);
                scope.Add(() => actor.HerbRecovered -= show); actor.HerbRecovered += show;
                scope.Add(() => actor.ExecutionRecovered -= show); actor.ExecutionRecovered += show;
            }
        }
        private void Show()
        {
            try { _effect.Play(new Vector3(_body.bounds.center.x, _body.bounds.min.y, _body.transform.position.z - .1f)); }
            catch (System.Exception error) { enabled = false; Debug.LogException(error, this); }
        }
        private void LateUpdate()
        {
            try
            {
                if (_effect == null || _body == null) throw new System.InvalidOperationException("회복 효과 표시 대상의 수명이 끝났습니다.");
                if (!_effect.IsPlaying) return;
                _effect.transform.position = new Vector3(_body.bounds.center.x, _body.bounds.min.y, _body.transform.position.z - .1f);
                _effect.Step(Time.deltaTime);
            }
            catch (System.Exception error) { enabled = false; Debug.LogException(error, this); }
        }
        private void OnDisable()
        {
            var scope = _scope; _scope = null;
            SubscriptionScope.Cleanup(() => scope?.Dispose(), () => Bind(null),
                () => { if (_effect != null) _effect.gameObject.SetActive(false); });
        }
    }
}
