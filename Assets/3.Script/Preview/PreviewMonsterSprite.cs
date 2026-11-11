using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay.Adapters;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    [DefaultExecutionOrder(10)]
    public class PreviewMonsterSprite : PreviewStateSprite
    {
        [SerializeField, InspectorName("일반몹 런타임")] private MonsterRuntime _monster = null;

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
            var pattern = actor.CurrentPattern;
            if (pattern != null && pattern.Key == state)
                return TryMapAttackTime(state, animation, 0, pattern.Attack, actor.AttackElapsed, actor.IsHolding, out time);
            time = 0f; return false;
        }

        protected override bool TryGetState(out StateKey state)
        {
            state = _monster != null && _monster.Actor != null ? _monster.Actor.CurrentState : StateKey.Idle;
            return _monster != null && _monster.Actor != null;
        }
    }
}
