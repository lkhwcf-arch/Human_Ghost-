using HumanGhost.Data.Definitions;
using UnityEngine;

namespace HumanGhost.Gameplay.Preview
{
    // 확인용 문자열·색상만 관리합니다. 상태를 변경하거나 입력·타격을 실행하지 않습니다.
    internal class PreviewPresentation
    {
        private PlayerActor _playerSource;
        private BossPoise _bossSource;
        private SubscriptionScope _playerScope, _bossScope;
        public void Reset()
        {
            _playerScope?.Dispose(); _bossScope?.Dispose();
            _playerScope = _bossScope = null; _playerSource = null; _bossSource = null;
        }
        // 스프라이트의 보이는 맨 아래(피벗 기준)입니다. 프레임마다 다른 아래 여백을 맞출 때 씁니다.
        public static float VisibleBottom(Sprite sprite)
        {
            float bottom = float.PositiveInfinity;
            foreach (Vector2 vertex in sprite.vertices) bottom = Mathf.Min(bottom, vertex.y);
            return bottom;
        }

        public void RefreshPlayer(PlayerActor actor, TextMesh label, SpriteRenderer body, Color color)
        {
            if (_playerSource != actor)
            {
                _playerScope?.Dispose(); _playerScope = new SubscriptionScope(); _playerSource = actor;
                System.Action invalidate = _playerScope.Invalidate;
                _playerScope.Add(() => actor.DisplayChanged -= invalidate);
                actor.DisplayChanged += invalidate;
                _playerScope.Invalidate();
            }
            if (_playerScope.TryConsumeRefresh())
            {
                SetText(label, "플레이어 " + actor.Definition.ID + "\n" + GetStateLabel(actor.CurrentState) +
                    "\n체력 " + actor.Health + "\n스태미나 " + actor.Stamina.Current +
                    (actor.CurrentState == StateKey.Bound ? "\n공격 연타 " + actor.BindPressesRemaining : "") +
                    (actor.HasSkill ? "\n결박 가르기 " + (actor.SkillCooldownSeconds > 0 ? actor.SkillCooldownSeconds + "초" : "가능") : "") +
                    (actor.HasHerbs ? "\n약초 " + actor.HerbCount : ""));
            }
            body.color = actor.IsDead ? Color.gray : actor.IsInvulnerable ?
                new Color(0.95f, 0.77f, 0.2f) : actor.IsAttackActive ? new Color(1f, 0.55f, 0.2f) : color;
        }

        public void RefreshBoss(BossPoise actor, TextMesh label, SpriteRenderer body, Color color)
        {
            if (_bossSource != actor)
            {
                _bossScope?.Dispose(); _bossScope = new SubscriptionScope(); _bossSource = actor;
                SubscriptionScope scope = _bossScope;
                System.Action invalidate = scope.Invalidate;
                System.Action<StateKey> stateChanged = value => scope.Invalidate();
                scope.Add(() => actor.Changed -= invalidate); actor.Changed += invalidate;
                scope.Add(() => actor.States.Changed -= stateChanged); actor.States.Changed += stateChanged;
                scope.Invalidate();
            }
            if (_bossScope.TryConsumeRefresh())
            {
                SetText(label, "보스 " + actor.Definition.ID + " · 페이즈 " + (actor.Phase.Phase + 1) + "\n" + GetStateLabel(actor.CurrentState) +
                    "\n체력 " + actor.Health + "\n체세 " + actor.Poise);
            }
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
                case StateKey.Attack4: return "공격 4타";
                case StateKey.Evade: return "회피";
                case StateKey.CritHit: return "치명타 피격";
                case StateKey.Skill1: return "결박 가르기";
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
