using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay.Adapters;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    [DefaultExecutionOrder(10)]
    public class PreviewMonsterSprite : PreviewStateSprite
    {
        [SerializeField, InspectorName("일반몹 런타임")] private MonsterRuntime _monster = null;
        [SerializeField, InspectorName("이 대상의 플레이어 처형 모션 (선택)")] private AnimationClip _playerExecution = null;
        public AnimationClip PlayerExecution => _playerExecution;
        [SerializeField, InspectorName("처형 시 바닥 기준 몸체 (선택)")] private SpriteRenderer _executionGround = null;

        private MonsterActor _actor;
        protected override void OnEnable()
        {
            base.OnEnable();
            if (_monster == null) return;
            _monster.Initialized += OnInitialized;
            Bind();
        }
        private void OnInitialized(Monster definition) => Bind();
        private void Bind()
        {
            if (_actor != null) _actor.Parried -= OnParried;
            _actor = _monster.Actor;
            if (_actor != null) _actor.Parried += OnParried;
        }
        private void OnDisable()
        {
            if (_monster != null) _monster.Initialized -= OnInitialized;
            if (_actor != null) _actor.Parried -= OnParried;
            _actor = null;
        }
        private void OnParried() => RestartAnimation();

        protected override void LateUpdate()
        {
            base.LateUpdate();
            var actor = _monster != null ? _monster.Actor : null;
            if (_executionGround == null || actor == null || (!actor.IsInvulnerable && !actor.WasExecuted) ||
                Renderer == null || Renderer.sprite == null) return;
            float offset = _executionGround.bounds.min.y - Renderer.bounds.min.y;
            if (Mathf.Abs(offset) > 0.00001f) Renderer.transform.position += Vector3.up * offset;
        }

        protected override float Duration(StateKey state)
        {
            var pattern = _monster.Actor.CurrentPattern;
            if (pattern != null && pattern.Key == state) return pattern.Attack.TotalTime / 1000f;
            if (state == StateKey.Stun) return _monster.Actor.StunDuration / 1000f;
            if (state == StateKey.Hit) return _monster.Patterns.HitStun / 1000f;
            if (state == StateKey.CritHit) return _monster.Patterns.CritHitStun / 1000f;
            return 0f;
        }

        protected override bool TryGetAnimationTime(StateKey state, AnimationClip animation, out float time)
        {
            var actor = _monster.Actor;
            if (actor.IsDead && actor.WasExecuted) { time = animation.length; return true; }
            var pattern = actor.CurrentPattern;
            if (pattern != null && pattern.Key == state)
                return TryMapAttackTime(state, animation, 0, pattern.Attack, actor.AttackElapsed, actor.IsHolding, out time);
            time = 0f; return false;
        }

        protected override bool TryGetState(out StateKey state)
        {
            state = _monster != null && _monster.Actor != null ? _monster.Actor.CurrentState : StateKey.Idle;
            // 사망 판정은 그대로 두고 처형 시체는 해체 마지막 자세를 유지합니다.
            if (state == StateKey.Dead && _monster.Actor.WasExecuted) state = StateKey.Execute;
            return _monster != null && _monster.Actor != null;
        }
    }
}
