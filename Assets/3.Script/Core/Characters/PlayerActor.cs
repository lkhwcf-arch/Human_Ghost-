using System;
using System.Collections.Generic;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay.States;

namespace HumanGhost.Gameplay
{
    // 위치·히트박스·입력을 소유하지 않는 플레이어 전투/행동 런타임입니다.
    public sealed class PlayerActor
    {
        private readonly AttackSpec[] _attacks;
        private readonly HashSet<int> _targets = new HashSet<int>();
        private int _attackIndex = -1;
        private int _elapsed;
        private int _duration;
        private int _invulnerableRemaining;
        private int _facing = 1;
        private bool _queued;
        private bool _attackActive;
        private BossPoise _executionTarget;

        public Player Definition { get; }
        public ActorStateMachine States { get; }
        public PlayerLocomotion Locomotion { get; }
        public PlayerStamina Stamina { get; }
        public int Health { get; private set; }
        public StateKey CurrentState => States.Current;
        public bool IsDead => Health == 0;
        public bool IsAttacking => _attackIndex >= 0;
        public bool IsAttackActive => _attackActive;
        public AttackSpec CurrentAttack => IsAttacking ? _attacks[_attackIndex] : null;
        public bool IsInvulnerable => CurrentState == StateKey.Execute || _invulnerableRemaining > 0 ||
            (CurrentState == StateKey.Roll && _elapsed < Definition.RollIFrame);
        public event Action<int> HealthChanged;
        public event Action<int> AttackStarted;
        public event Action<bool> AttackWindowChanged;
        public event Action<int, int, int> RollRequested;

        // attacks = 지상 1/2/3타, 공중 1타 순서. 초기 자원은 호출자가 명시합니다.
        public PlayerActor(Player definition, IReadOnlyList<AttackSpec> attacks, bool grounded, int currentStamina)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (definition.HP <= 0 || definition.RollTime < 0 || definition.RollIFrame < 0 || definition.RollIFrame > definition.RollTime ||
                definition.HitStunTime < 0 || definition.CritHitStunTime < 0 || definition.HitIFrame < 0 || definition.InputBuffer < 0)
                throw new ArgumentException("플레이어 HP·행동 시간 범위를 확인하세요.", nameof(definition));
            if (attacks == null || attacks.Count != 4) throw new ArgumentException("지상 3타와 공중 1타 설정이 필요합니다.", nameof(attacks));
            _attacks = new AttackSpec[4];
            for (int i = 0; i < 4; i++) _attacks[i] = attacks[i] ?? throw new ArgumentException("빈 공격 설정입니다.", nameof(attacks));
            States = new ActorStateMachine(new IdleState());
            Locomotion = new PlayerLocomotion(definition, grounded, States);
            Stamina = new PlayerStamina(definition, currentStamina);
            Health = definition.HP;
        }

        public void SetMove(int direction)
        {
            Locomotion.SetMove(direction);
            if (direction != 0) _facing = direction;
        }

        public bool TryJump() => !IsDead && Locomotion.TryJump();

        public bool TryAttack()
        {
            if (IsDead) return false;
            if (IsAttacking)
            {
                if (_attackIndex >= 2 || CurrentAttack.ComboWindow == 0) return false;
                AttackSpec spec = CurrentAttack;
                int start = spec.Windup + spec.Active;
                // 선입력 보존 시간과 콤보 입력 창은 별개입니다.
                if ((_elapsed < start && start - _elapsed <= Definition.InputBuffer) ||
                    (_elapsed >= start && _elapsed < start + spec.ComboWindow))
                { _queued = true; return true; }
                return false;
            }
            if (!Locomotion.CanMove) return false;
            return StartAttack(Locomotion.IsGrounded ? 0 : 3);
        }

        public bool TryRoll()
        {
            if (IsDead || (IsAttacking ? _elapsed < CurrentAttack.Windup + CurrentAttack.Active : !Locomotion.CanMove)) return false;
            if (!Stamina.TryConsumeRoll()) return false;
            ClearAttack();
            _elapsed = 0; _duration = Definition.RollTime;
            States.Call(StateKey.Roll);
            RollRequested?.Invoke(_facing, Definition.RollDistance, Definition.RollTime);
            if (_duration == 0) Locomotion.Resume();
            return true;
        }

        // 외부 판정이 확정한 HP 피해를 받습니다. 회피·방어 계산은 CombatMath/판정자 책임입니다.
        public bool ReceiveHit(int damage, bool critical = false)
        {
            if (damage < 0) throw new ArgumentOutOfRangeException(nameof(damage));
            if (IsDead || IsInvulnerable || damage == 0) return false;
            Health = Math.Max(0, Health - damage);
            ClearAttack();
            _elapsed = 0;
            _invulnerableRemaining = Definition.HitIFrame;
            if (IsDead) States.Change(new DeadState());
            else
            {
                _duration = critical ? Definition.CritHitStunTime : Definition.HitStunTime;
                States.Change(new HitState(critical));
                if (_duration == 0) Locomotion.Resume();
            }
            HealthChanged?.Invoke(Health);
            return true;
        }

        // 판정 창마다 대상 ID를 한 번만 허용합니다. 충돌 위치/대상 선택은 외부에서 공급합니다.
        public bool TryRegisterTarget(int targetId)
        {
            if (targetId <= 0) throw new ArgumentOutOfRangeException(nameof(targetId));
            return IsAttackActive && _targets.Add(targetId);
        }

        // 충돌 판정자가 공급한 대상과 회피/치명 판정으로 보스 피해를 한 번 적용합니다.
        public bool TryHitBoss(int targetId, BossPoise target, bool dodged, bool critical)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (target.IsDead || target.IsInvulnerable || !TryRegisterTarget(targetId)) return false;
            if (dodged) return false;
            return target.ReceiveHit(CombatMath.Damage(Definition, target.Definition, CurrentAttack.ExtraDamage, critical), CurrentAttack.PoiseDamage);
        }

        public bool TryExecute(BossPoise target, bool inRange)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (!inRange || IsDead || !Locomotion.CanMove || !target.TryBeginExecution()) return false;
            _executionTarget = target;
            States.Change(new ExecuteState());
            return true;
        }

        // 해체 후 체세 회복 정책은 호출자가 회의 결과에 따라 명시합니다. 기본값 없음.
        public bool CompleteExecution(bool restorePoise)
        {
            if (CurrentState != StateKey.Execute || _executionTarget == null) return false;
            _executionTarget.CompleteExecution(restorePoise);
            _executionTarget = null;
            Locomotion.Resume();
            return true;
        }

        public void Tick(int milliseconds)
        {
            if (milliseconds < 0) throw new ArgumentOutOfRangeException(nameof(milliseconds));
            if (IsDead || CurrentState == StateKey.Execute) return;
            while (milliseconds > 0)
            {
                bool timed = IsAttacking || CurrentState == StateKey.Roll || CurrentState == StateKey.Hit || CurrentState == StateKey.CritHit;
                if (!timed) { AdvanceTime(milliseconds); break; }
                AttackSpec spec = CurrentAttack;
                int end = IsAttacking ? (_elapsed < spec.Windup ? spec.Windup : _elapsed < spec.Windup + spec.Active ? spec.Windup + spec.Active : spec.TotalTime) : _duration;
                int step = Math.Min(milliseconds, end - _elapsed);
                AdvanceTime(step);
                _elapsed += step; milliseconds -= step;
                if (IsAttacking)
                {
                    if (_elapsed == spec.Windup) SetAttackActive(true);
                    if (_elapsed == spec.Windup + spec.Active) SetAttackActive(false);
                    if (_elapsed == spec.TotalTime)
                    {
                        int next = _attackIndex + 1;
                        bool chain = _queued && _attackIndex < 2;
                        ClearAttack();
                        if (!chain || !StartAttack(next)) Locomotion.Resume();
                    }
                }
                else if (_elapsed == _duration) Locomotion.Resume();
            }
        }

        private void AdvanceTime(int milliseconds)
        {
            Stamina.Tick(milliseconds);
            _invulnerableRemaining = Math.Max(0, _invulnerableRemaining - milliseconds);
        }

        private bool StartAttack(int index)
        {
            if (!Stamina.TryConsume(_attacks[index].StaminaCost)) return false;
            _attackIndex = index; _elapsed = 0; _queued = false; _targets.Clear();
            States.Change(new AttackState(index == 3 ? StateKey.Attack1 : (index == 0 ? StateKey.Attack1 : index == 1 ? StateKey.Attack2 : StateKey.Attack3)));
            AttackStarted?.Invoke(index);
            return true;
        }

        private void ClearAttack()
        {
            SetAttackActive(false);
            _attackIndex = -1; _queued = false; _targets.Clear();
        }

        private void SetAttackActive(bool active)
        {
            if (_attackActive == active) return;
            _attackActive = active;
            AttackWindowChanged?.Invoke(active);
        }
    }
}
