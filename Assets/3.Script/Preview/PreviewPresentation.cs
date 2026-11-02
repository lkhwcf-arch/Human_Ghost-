using HumanGhost.Data.Definitions;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 확인용 문자열·색상만 관리합니다. 상태를 변경하거나 입력·타격을 실행하지 않습니다.
    internal static class PreviewPresentation
    {
        public static void RefreshPlayer(PlayerActor actor, TextMesh label, SpriteRenderer body, Color color)
        {
            SetText(label, "플레이어 " + actor.Definition.ID + "\n" + GetStateLabel(actor.CurrentState) +
                "\n체력 " + actor.Health + "\n스태미나 " + actor.Stamina.Current +
                (actor.BoundBy != null ? "\n공격 연타 " + actor.BindPressesRemaining : ""));
            body.color = actor.IsDead ? Color.gray : actor.IsInvulnerable ?
                new Color(0.95f, 0.77f, 0.2f) : actor.IsAttackActive ? new Color(1f, 0.55f, 0.2f) : color;
        }

        public static void RefreshBoss(BossPoise actor, TextMesh label, SpriteRenderer body, Color color)
        {
            SetText(label, "보스 " + actor.Definition.ID + "\n" + GetStateLabel(actor.CurrentState) +
                "\n체력 " + actor.Health + "\n체세 " + actor.Poise);
            body.color = actor.IsDead ? Color.gray : actor.IsExposed ?
                new Color(0.75f, 0.35f, 0.95f) : actor.IsInvulnerable ? Color.yellow : color;
        }

        public static string GetStateLabel(StateKey state)
        {
            switch (state)
            {
                case StateKey.Idle: return "대기";
                case StateKey.Walk: return "이동";
                case StateKey.Hit: return "피격";
                case StateKey.Attack1: return "공격 1타";
                case StateKey.Attack2: return "공격 2타";
                case StateKey.Attack3: return "공격 3타";
                case StateKey.Evade: return "회피";
                case StateKey.CritHit: return "치명타 피격";
                case StateKey.Skill1: return "기술 1";
                case StateKey.Hang: return "매달리기";
                case StateKey.Land: return "착지";
                case StateKey.Roll: return "구르기";
                case StateKey.Jump: return "점프";
                case StateKey.Exposed: return "급소 노출";
                case StateKey.Execute: return "해체";
                case StateKey.Dead: return "사망";
                case StateKey.Heal: return "회복";
                case StateKey.Acquire: return "획득";
                case StateKey.Stun: return "기절";
                case StateKey.Skill2: return "기술 2";
                case StateKey.Skill3: return "기술 3";
                case StateKey.Bound: return "구속";
                case StateKey.PhaseChange: return "단계 전환";
                default: return "알 수 없음";
            }
        }

        private static void SetText(TextMesh label, string value)
        {
            if (label.text != value) label.text = value;
        }
    }
}
