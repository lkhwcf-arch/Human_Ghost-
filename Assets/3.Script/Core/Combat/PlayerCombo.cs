using System;
using System.Collections.Generic;

namespace HumanGhost.Gameplay
{
    // 공격 설정 스냅샷·타수·콤보 입력만 소유합니다. 시간 진행·소모·상태 전환은 PlayerActor 책임입니다.
    internal sealed class PlayerCombo
    {
        private readonly AttackSpec[] _attacks;
        private readonly AttackSpec[] _originalAttacks;
        private AttackSpec _current;
        private int _timeReduction;
        public int Index { get; private set; } = -1;
        public bool Queued { get; private set; }
        public AttackSpec Current => Index >= 0 ? _current : null;

        public PlayerCombo(IReadOnlyList<AttackSpec> attacks)
        {
            if (attacks == null || attacks.Count != 5) throw new ArgumentException("지상 4타와 공중 1타 설정이 필요합니다.", nameof(attacks));
            _attacks = new AttackSpec[5];
            for (int i = 0; i < _attacks.Length; i++)
                _attacks[i] = attacks[i] ?? throw new ArgumentException("빈 공격 설정입니다.", nameof(attacks));
            _originalAttacks = (AttackSpec[])_attacks.Clone();
        }

        public void SetTimeReduction(int reduction)
        {
            reduction = Math.Max(0, Math.Min(10000, reduction));
            if (_timeReduction == reduction) return;
            _timeReduction = reduction;
            int Scale(int time) => (int)(((long)time * (10000 - reduction) + 5000) / 10000);
            for (int i = 0; i < _attacks.Length; i++)
            {
                AttackSpec original = _originalAttacks[i];
                _attacks[i] = new AttackSpec(Math.Max(1, Scale(original.Windup)), Math.Max(1, Scale(original.Active)),
                    Scale(original.Recovery), Math.Min(Scale(original.Recovery), Scale(original.ComboWindow)),
                    original.ExtraDamage, original.PoiseDamage, original.StaminaCost);
            }
        }

        public AttackSpec GetAttack(int index) => _attacks[index];

        public void Start(int index)
        {
            if (index < 0 || index >= _attacks.Length) throw new ArgumentOutOfRangeException(nameof(index));
            Index = index;
            _current = _attacks[index];
            Queued = false;
        }

        public bool TryQueue(int elapsed, int inputBuffer)
        {
            if (Index < 0 || Index >= 3 || Current.ComboWindow == 0) return false;
            int start = Current.Windup + Current.Active;
            // 선입력 보존 시간과 후딜의 콤보 입력 창은 별개입니다.
            if ((elapsed < start && start - elapsed <= inputBuffer) ||
                (elapsed >= start && elapsed < start + Current.ComboWindow))
            { Queued = true; return true; }
            return false;
        }

        public void DiscardBufferedInput() => Queued = false;

        public void Clear()
        {
            Index = -1;
            Queued = false;
        }
    }
}
