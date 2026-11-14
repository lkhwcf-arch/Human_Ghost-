using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay.Adapters;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    [DefaultExecutionOrder(10)]
    public sealed class PreviewBossSprite : PreviewStateSprite
    {
        [SerializeField, InspectorName("보스 런타임")] private BossRuntime _boss = null;
        [SerializeField, InspectorName("판정 사각형 (색 따라감)")] private SpriteRenderer _body = null;
        [SerializeField, InspectorName("몸통 기준 가로 보정 (타일)")] private float _bodyOffsetX = 0.87f;
        [SerializeField] private BossPhaseTwoView _phaseTwo = null;
        protected override bool AlignFeet => false;
        private readonly System.Collections.Generic.Dictionary<Sprite, float> _spriteTops = new System.Collections.Generic.Dictionary<Sprite, float>();

        // 지금 화면에 보이는 보스 그림의 가장 높은 월드 y입니다(1페이즈는 투명 여백을 뺀 스프라이트 윤곽, 2페이즈는 리그). A4 플레이어 공격 판정 높이에만 씁니다.
        public float VisibleTop()
        {
            if (_phaseTwo != null && _boss.Actor != null && _boss.Actor.Phase.Phase > 0 && !_boss.Actor.IsTransitioning) return _phaseTwo.VisibleTop();
            Sprite sprite = Renderer.sprite;
            if (!Renderer.enabled || sprite == null) return float.NegativeInfinity;
            if (!_spriteTops.TryGetValue(sprite, out float localTop))
            {
                localTop = float.NegativeInfinity;
                foreach (Vector2 vertex in sprite.vertices) localTop = Mathf.Max(localTop, vertex.y);
                _spriteTops.Add(sprite, localTop);
            }
            return Renderer.transform.TransformPoint(new Vector3(0f, localTop, 0f)).y;
        }
        protected override void LateUpdate()
        {
            base.LateUpdate();
            GroundCurrentFrame(_body);
            if (_phaseTwo == null || _boss.Actor == null) return;
            bool second = _boss.Actor.Phase.Phase > 0 && !_boss.Actor.IsTransitioning;
            Renderer.enabled = !second;
            _phaseTwo.Show(second);
        }
        protected override bool TryGetState(out StateKey state)
        {
            state = _boss != null && _boss.Actor != null ? _boss.Actor.CurrentState : StateKey.Idle;
            return _boss != null && _boss.Actor != null;
        }
        protected override float Duration(StateKey state)
        {
            if (_boss.Combat == null || !_boss.Combat.IsAttacking) return 0f;
            int total = 0;
            foreach (AttackSpec hit in _boss.Combat.CurrentPattern.Hits) total += hit.TotalTime;
            return total / 1000f;
        }
        protected override bool TryGetAnimationTime(StateKey state, AnimationClip animation, out float time)
        {
            var combat = _boss.Combat;
            if (combat != null && combat.IsAttacking && combat.CurrentPattern.Key == state)
                return TryMapAttackTime(state, animation, combat.HitIndex, combat.CurrentPattern.Hits[combat.HitIndex],
                    combat.HitElapsed, combat.PullVelocity != 0, out time);
            time = 0f; return false;
        }
        protected override void UpdateFacing()
        {
            var actor = _boss.Actor;
            bool attacking = _boss.Combat != null && _boss.Combat.IsAttacking;
            if (_body != null) Renderer.color = attacking || actor.IsExposed || actor.IsInvulnerable || actor.IsDead ? _body.color : Color.white;
            int facing = _boss.Combat != null ? _boss.Combat.Facing : 1;
            Renderer.flipX = facing < 0;
            Vector3 position = Renderer.transform.localPosition;
            position.x = facing * _bodyOffsetX;
            Renderer.transform.localPosition = position;
        }
    }
}
