using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay.Adapters;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    [DefaultExecutionOrder(-10)]
    public sealed class PreviewBossSprite : PreviewStateSprite
    {
        [SerializeField, InspectorName("보스 런타임")] private BossRuntime _boss = null;
        [SerializeField, InspectorName("판정 사각형 (색 따라감)")] private SpriteRenderer _body = null;
        [SerializeField, InspectorName("몸통 기준 가로 보정 (타일)")] private float _bodyOffsetX = 0.87f;
        protected override bool AlignFeet => false;
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
