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
        [SerializeField, InspectorName("투사체 그림 (선택)")] private Sprite _projectileSprite = null;
        public Sprite ProjectileSprite => _projectileSprite;
        [SerializeField, InspectorName("처형 시 바닥 기준 몸체 (선택)")] private SpriteRenderer _executionGround = null;

        private MonsterActor _actor;
        // 뒤로 깡총(옹귀 1117) 표시: 준비=웅크림, 판정 창=공중 포물선, 후딜=착지 찌그러짐. 위치 판정은 코어 이동만 씁니다.
        // [정수화 미적용 이유] 깡총·처형 연출의 Transform 높이·크기 표시값입니다.
        [SerializeField, InspectorName("깡총 높이 (월드)"), Min(0f)] private float _hopHeight = 1f;
        private Vector3 _hopBaseScale;
        private float _hopOffset;
        private bool _hopScaled;
        private SubscriptionScope _runtimeScope, _actorScope;
        protected override void OnEnable()
        {
            base.OnEnable();
            if (_monster == null) return;
            var runtime = _monster;
            var scope = _runtimeScope = new SubscriptionScope();
            System.Action<Monster> initialized = scope.Guard<Monster>(OnInitialized);
            scope.Add(() => runtime.Initialized -= initialized); runtime.Initialized += initialized;
            Bind();
        }
        private void OnInitialized(Monster definition) => Bind();
        private void Bind()
        {
            _actorScope?.Dispose(); _actorScope = null;
            _actor = _monster.Actor;
            if (_actor != null)
            {
                var actor = _actor;
                var scope = _actorScope = new SubscriptionScope();
                System.Action parried = scope.Guard(OnParried);
                scope.Add(() => actor.Parried -= parried); actor.Parried += parried;
            }
        }
        private void OnDisable()
        {
            var runtimeScope = _runtimeScope; var actorScope = _actorScope;
            _runtimeScope = null; _actorScope = null;
            _actor = null;
            SubscriptionScope.Cleanup(() => runtimeScope?.Dispose(), () => actorScope?.Dispose());
        }
        private void OnParried() => RestartAnimation();

        protected override void LateUpdate()
        {
            if (Renderer != null && _hopOffset != 0f) { Renderer.transform.position -= Vector3.up * _hopOffset; _hopOffset = 0f; }
            if (Renderer != null && _hopScaled) { Renderer.transform.localScale = _hopBaseScale; _hopScaled = false; }
            base.LateUpdate();
            Ground();
            ApplyHop();
        }
        private void Ground()
        {
            var actor = _monster != null ? _monster.Actor : null;
            // Preserve the airborne arc of ordinary death/evasion. Executed remains stay grounded.
            if (actor != null && (actor.WasExecuted || (!actor.IsDead && actor.CurrentState != StateKey.Evade)) &&
                GroundCurrentFrame(_executionGround)) return;
            if (_executionGround == null || actor == null || (!actor.IsInvulnerable && !actor.WasExecuted) ||
                Renderer == null || Renderer.sprite == null) return;
            float offset = _executionGround.bounds.min.y - Renderer.bounds.min.y;
            if (Mathf.Abs(offset) > 0.00001f) Renderer.transform.position += Vector3.up * offset;
        }

        private void ApplyHop()
        {
            var actor = _monster != null ? _monster.Actor : null;
            if (Renderer == null || actor == null || !actor.IsRetreating || actor.IsDead) return;
            AttackSpec spec = actor.CurrentPattern.Attack;
            int elapsed = actor.AttackElapsed;
            Vector2 squash = Vector2.one;
            if (elapsed < spec.Windup) squash = new Vector2(1.1f, .85f);
            else if (elapsed < spec.Windup + spec.Active)
            {
                float t = (elapsed - spec.Windup) / (float)spec.Active;
                _hopOffset = 4f * _hopHeight * t * (1f - t);
                squash = new Vector2(.95f, 1.08f);
            }
            else squash = new Vector2(1.15f, .8f);
            _hopBaseScale = Renderer.transform.localScale; _hopScaled = true;
            // 발 높이를 유지한 채 찌그러뜨립니다.
            float bottom = Renderer.bounds.min.y;
            Renderer.transform.localScale = new Vector3(_hopBaseScale.x * squash.x, _hopBaseScale.y * squash.y, _hopBaseScale.z);
            _hopOffset += bottom - Renderer.bounds.min.y;
            Renderer.transform.position += Vector3.up * _hopOffset;
        }

        protected override int DurationMilliseconds(StateKey state)
        {
            var pattern = _monster.Actor.CurrentPattern;
            if (pattern != null && pattern.Key == state) return pattern.Attack.TotalTime;
            if (state == StateKey.Stun) return _monster.Actor.StunDuration;
            if (state == StateKey.Hit) return _monster.Definition.HitStun;
            if (state == StateKey.CritHit) return _monster.Definition.CritHitStun;
            return 0;
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
