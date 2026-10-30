using System;

namespace HumanGhost.Gameplay
{
    // 공격 한 동작의 읽기 전용 실행 인수입니다. 초기 값·튜닝은 설정 에셋에서 받습니다.
    public sealed class AttackSpec
    {
        public int Windup { get; }
        public int Active { get; }
        public int Recovery { get; }
        public int ComboWindow { get; }
        public int ExtraDamage { get; }
        public int PoiseDamage { get; }
        public int StaminaCost { get; }
        public int TotalTime { get; }

        public AttackSpec(int windup, int active, int recovery, int comboWindow, int extraDamage, int poiseDamage, int staminaCost)
        {
            if (windup <= 0 || active <= 0 || recovery < 0 || comboWindow < 0 || comboWindow > recovery || poiseDamage < 0 || staminaCost < 0)
                throw new ArgumentOutOfRangeException(nameof(windup), "공격 시간·소모·체세 피해 범위를 확인하세요.");
            TotalTime = checked(windup + active + recovery);
            Windup = windup; Active = active; Recovery = recovery; ComboWindow = comboWindow;
            ExtraDamage = extraDamage; PoiseDamage = poiseDamage; StaminaCost = staminaCost;
        }
    }
}
