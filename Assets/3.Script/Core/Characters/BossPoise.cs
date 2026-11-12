using System;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay.States;

namespace HumanGhost.Gameplay
{
    // 일반몹·보스의 해체 계약입니다. 거리·입력·연출 시간은 호출자가 소유합니다.
    public interface IExecutionTarget
    {
        bool IsExposed { get; }
        bool TryBeginExecution();
        bool CompleteExecution(bool restorePoise);
        bool CancelExecution();
    }

    // 보스의 체세·해체 수치입니다. 거리/연출은 외부 책임입니다.
    // 페이즈: 시작 행보다 뒤인 BossState 행의 HPThreshold 이하로 HP가 내려가면 그 행으로 넘어갑니다(되돌아가지 않음).
    // 전환은 수치 행만 바꿉니다. 진행 중인 노출 시간·패턴은 유지하고 다음 대기·회복·노출·해체부터 새 행 값을 씁니다.
    public sealed class BossPoise : IExecutionTarget
    {
        private const int _ratioScale = 10000;
        private int _sinceHit;
        private int _remainder;
        private int _exposedRemaining;
        public Boss Definition { get; }
        public BossState Phase { get; private set; }
        public int Health { get; private set; }
        public int Poise { get; private set; }
        public ActorStateMachine States { get; } = new ActorStateMachine(new IdleState());
        public StateKey CurrentState => States.Current;
        public bool IsDead => Health == 0;
        public bool IsExposed => CurrentState == StateKey.Exposed;
        public bool IsInvulnerable => CurrentState == StateKey.Execute;
        public event Action Changed;
        // 페이즈 행이 바뀐 직후, 같은 호출의 Changed보다 먼저 알립니다. 포효 등 연출 연결용입니다.
        public event Action<BossState> PhaseChanged;

        public BossPoise(Boss definition, BossState phase)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Phase = phase ?? throw new ArgumentNullException(nameof(phase));
            if (definition.HP <= 0 || definition.Poise <= 0) throw new ArgumentException("보스 체세·해체 정의를 확인하세요.", nameof(definition));
            Validate(phase);
            // 뒤 페이즈일수록 낮은 HP 비율에서 시작해야 전환 순서가 하나로 정해집니다(행 순서와 무관).
            foreach (BossState a in definition.States)
            {
                if (a == null || a.Phase < phase.Phase || a == phase) continue;
                Validate(a);
                foreach (BossState b in definition.States)
                    if (b != null && b != a && b.Phase >= phase.Phase && (b.Phase == a.Phase || (b.Phase < a.Phase) != (b.HPThreshold > a.HPThreshold)))
                        throw new ArgumentException("보스 페이즈 HP 기준을 확인하세요.", nameof(definition));
                if (a.HPThreshold <= 0) throw new ArgumentException("보스 페이즈 HP 기준을 확인하세요.", nameof(definition));
            }
            Health = definition.HP; Poise = definition.Poise;
            _sinceHit = phase.PoiseRegenDelay;
        }

        private void Validate(BossState phase)
        {
            if (phase.UnitID != Definition.ID || phase.IdleMin < 0 || phase.IdleMax < phase.IdleMin || phase.PoiseRegenDelay < 0 ||
                phase.PoiseRegen < 0 || phase.ExposeTime <= 0 || phase.ExecuteDamage < 0 || phase.ExecuteDamage > _ratioScale ||
                phase.ExecuteType != "DAMAGE" || phase.HPThreshold > _ratioScale)
                throw new ArgumentException("보스 체세·해체 정의를 확인하세요.", nameof(phase));
        }

        // HP가 줄어든 직후 호출. 사망이면 전환하지 않습니다. 한 번에 여러 기준을 넘으면 가장 뒤 행으로 바로 갑니다.
        private bool UpdatePhase()
        {
            if (IsDead) return false;
            BossState target = Phase;
            foreach (BossState next in Definition.States)
                if (next != null && next.Phase > target.Phase && (long)Health * _ratioScale <= (long)next.HPThreshold * Definition.HP) target = next;
            if (target == Phase) return false;
            Phase = target;
            return true;
        }

        public bool ReceiveHit(int damage, int poiseDamage)
        {
            if (damage < 0 || poiseDamage < 0) throw new ArgumentOutOfRangeException(nameof(damage));
            if (IsDead || IsInvulnerable || (damage == 0 && poiseDamage == 0)) return false;
            Health = Math.Max(0, Health - damage);
            Poise = Math.Max(0, Poise - poiseDamage);
            _sinceHit = 0; _remainder = 0;
            bool phaseChanged = UpdatePhase();
            if (IsDead) States.Change(new DeadState());
            else if (Poise == 0 && !IsExposed)
            {
                _exposedRemaining = Phase.ExposeTime;
                States.Call(StateKey.Exposed);
            }
            Notify(phaseChanged);
            return true;
        }

        private void Notify(bool phaseChanged)
        {
            if (phaseChanged) PhaseChanged?.Invoke(Phase);
            Changed?.Invoke();
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
            // 해체 피해는 해체를 시작한 페이즈 값입니다.
            int damage = (int)((long)Definition.HP * Phase.ExecuteDamage / 10000);
            Health = Math.Max(0, Health - damage);
            if (restorePoise) Poise = Definition.Poise;
            _sinceHit = 0; _remainder = 0;
            bool phaseChanged = UpdatePhase();
            if (IsDead) States.Change(new DeadState());
            else if (restorePoise) States.Change(new IdleState());
            else { _exposedRemaining = Phase.ExposeTime; States.Call(StateKey.Exposed); }
            Notify(phaseChanged);
            return true;
        }

        public bool CancelExecution()
        {
            if (!IsInvulnerable) return false;
            States.Call(StateKey.Exposed);
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
