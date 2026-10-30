using System;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay.States;

namespace HumanGhost.Gameplay
{
    // 지스타의 체세·해체 대상은 보스만입니다. 페이즈 선택/거리/연출은 외부 책임입니다.
    public sealed class BossPoise
    {
        private int _sinceHit;
        private int _remainder;
        private int _exposedRemaining;
        public Boss Definition { get; }
        public BossState Phase { get; }
        public int Health { get; private set; }
        public int Poise { get; private set; }
        public ActorStateMachine States { get; } = new ActorStateMachine(new IdleState());
        public StateKey CurrentState => States.Current;
        public bool IsDead => Health == 0;
        public bool IsExposed => CurrentState == StateKey.Exposed;
        public bool IsInvulnerable => CurrentState == StateKey.Execute;
        public event Action Changed;

        public BossPoise(Boss definition, BossState phase)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Phase = phase ?? throw new ArgumentNullException(nameof(phase));
            if (phase.UnitID != definition.ID || definition.HP <= 0 || definition.Poise <= 0 || phase.PoiseRegenDelay < 0 ||
                phase.PoiseRegen < 0 || phase.ExposeTime <= 0 || phase.ExecuteDamage < 0 || phase.ExecuteDamage > 10000 || phase.ExecuteType != "DAMAGE")
                throw new ArgumentException("보스 체세·해체 정의를 확인하세요.", nameof(phase));
            Health = definition.HP; Poise = definition.Poise;
            _sinceHit = phase.PoiseRegenDelay;
        }

        public bool ReceiveHit(int damage, int poiseDamage)
        {
            if (damage < 0 || poiseDamage < 0) throw new ArgumentOutOfRangeException(nameof(damage));
            if (IsDead || IsInvulnerable || (damage == 0 && poiseDamage == 0)) return false;
            Health = Math.Max(0, Health - damage);
            Poise = Math.Max(0, Poise - poiseDamage);
            _sinceHit = 0; _remainder = 0;
            if (IsDead) States.Change(new DeadState());
            else if (Poise == 0 && !IsExposed)
            {
                _exposedRemaining = Phase.ExposeTime;
                States.Call(StateKey.Exposed);
            }
            Changed?.Invoke();
            return true;
        }

        public bool TryBeginExecution()
        {
            if (IsDead || !IsExposed) return false;
            States.Change(new ExecuteState());
            Changed?.Invoke();
            return true;
        }

        public bool CompleteExecution(bool restorePoise)
        {
            if (CurrentState != StateKey.Execute) return false;
            int damage = (int)((long)Definition.HP * Phase.ExecuteDamage / 10000);
            Health = Math.Max(0, Health - damage);
            if (restorePoise) Poise = Definition.Poise;
            _sinceHit = 0; _remainder = 0;
            if (IsDead) States.Change(new DeadState());
            else if (restorePoise) States.Change(new IdleState());
            else { _exposedRemaining = Phase.ExposeTime; States.Call(StateKey.Exposed); }
            Changed?.Invoke();
            return true;
        }

        public void Tick(int milliseconds)
        {
            if (milliseconds < 0) throw new ArgumentOutOfRangeException(nameof(milliseconds));
            if (IsDead || IsInvulnerable || milliseconds == 0) return;
            if (IsExposed)
            {
                if (milliseconds < _exposedRemaining) { _exposedRemaining -= milliseconds; return; }
                milliseconds -= _exposedRemaining;
                Poise = Definition.Poise; _remainder = 0;
                States.Change(new IdleState());
                Changed?.Invoke();
            }
            int delayed = Math.Min(milliseconds, Phase.PoiseRegenDelay - _sinceHit);
            _sinceHit += delayed;
            if (Poise == Definition.Poise) { _remainder = 0; return; }
            long amount = (long)Phase.PoiseRegen * (milliseconds - delayed) + _remainder;
            int next = (int)Math.Min(Definition.Poise, Poise + amount / 1000);
            _remainder = next == Definition.Poise ? 0 : (int)(amount % 1000);
            if (next == Poise) return;
            Poise = next;
            Changed?.Invoke();
        }
    }
}
