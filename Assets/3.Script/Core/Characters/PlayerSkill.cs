using System;

namespace HumanGhost.Gameplay
{
    // 결박 가르기(C07) 한 개체의 실행 인수와 쿨타임만 소유합니다. 상태 전환·판정은 PlayerActor가 맡습니다.
    public sealed class PlayerSkill
    {
        private readonly int _cooldown;
        public AttackSpec Attack { get; }
        public int Distance { get; }
        public int Remaining { get; private set; }
        public bool IsReady => Remaining == 0;
        public int CooldownSeconds => Remaining / 1000 + (Remaining % 1000 > 0 ? 1 : 0);
        public event Action<int> CooldownSecondsChanged;

        public PlayerSkill(AttackSpec attack, int cooldown, int distance)
        {
            if (cooldown < 0 || distance < 0) throw new ArgumentOutOfRangeException(nameof(cooldown));
            Attack = attack ?? throw new ArgumentNullException(nameof(attack));
            _cooldown = cooldown; Distance = distance;
        }

        public void StartCooldown() => SetRemaining(_cooldown);
        public void Tick(int milliseconds) => SetRemaining(Math.Max(0, Remaining - milliseconds));
        private void SetRemaining(int value)
        {
            int previous = CooldownSeconds;
            Remaining = value;
            if (previous != CooldownSeconds) CooldownSecondsChanged?.Invoke(CooldownSeconds);
        }
    }
}
