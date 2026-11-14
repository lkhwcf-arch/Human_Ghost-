using System;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay.States;

namespace HumanGhost.Gameplay
{
    public sealed partial class PlayerActor
    {
        private PlayerParry _parry;
        private bool _parryLocked;
        public int Facing => _facing;
        public bool IsActionBusy => _parryLocked;
        public bool IsParrying => _parryLocked;
        public int ParryStunTime => _parry.StunTime;
        public int ParryPoiseDamage => _parry.PoiseDamage;
        public int ParryStaminaCost => _parry == null ? 0 : (int)Math.Max(0, Math.Min(int.MaxValue, (long)_parry.StaminaCost + _parryCostDelta));
        public event Action<string> ActionCue;
        public event Action<int> ParrySucceeded;

        public void ConfigureActions(PlayerAction actions)
        {
            var parry = new PlayerParry(actions);
            if (parry.PlayerId != Definition.ID || IsActionBusy) throw new ArgumentException("플레이어 패링 연결을 확인하세요.", nameof(actions));
            _parry = parry;
        }

        public bool TryParry()
        {
            if (_parry == null || IsDead || _cutsceneDamageBlocked || IsActionBusy || !Locomotion.IsGrounded) return false;
            if (IsAttacking)
            {
                int start = CurrentAttack.Windup + CurrentAttack.Active;
                if (_combo.Index >= 3 || _elapsed < start || _elapsed >= start + CurrentAttack.ComboWindow) return false;
            }
            else if (!Locomotion.CanMove) return false;
            if (!Stamina.TryConsume(ParryStaminaCost)) { StaminaRefused?.Invoke(); return false; }
            ClearAttack();
            _parry.TryBegin(); _parryLocked = true;
            Locomotion.SetActionBlocked(true);
            // 상태 등록은 별도 작업입니다. 패링 수명은 상태 ID 없이도 독립적으로 유지합니다.
            States.Change(new IdleState());
            ActionCue?.Invoke("Parry"); NotifyDisplayChanged();
            return true;
        }

        internal bool TryParryContact(AttackWindow incoming, int targetId, int direction, bool deferFeedback = false)
        {
            if (!IsParrying || IsDead || _cutsceneDamageBlocked || !_parry.TryDeflect(incoming, targetId, direction, _facing)) return false;
            if (!deferFeedback) CompleteParryContact();
            return true;
        }

        // 적의 공격 취소·체세 반영까지 끝난 뒤 표시 소비자에게 성공을 알립니다.
        internal void CompleteParryContact()
        {
            if (!_parry.IsActive) FinishAction();
            ParrySucceeded?.Invoke(_parry.HitStop);
        }

        private void TickAction(ref int milliseconds)
        {
            if (!IsActionBusy) return;
            int consumed = _parry.Tick(milliseconds);
            AdvanceTime(consumed); milliseconds -= consumed;
            if (!_parry.IsActive) FinishAction();
        }

        private void FinishAction() { CancelCombatAction(); ResumeMovement(); }

        public void CancelCombatAction()
        {
            bool active = _parryLocked;
            _parry?.Cancel(); _parryLocked = false;
            Locomotion.SetActionBlocked(false);
            if (active) { ActionCue?.Invoke("ActionExit"); NotifyDisplayChanged(); }
        }
    }
}
