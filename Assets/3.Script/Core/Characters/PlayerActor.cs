using System;
using System.Collections.Generic;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay.States;

namespace HumanGhost.Gameplay
{
    // 위치·히트박스·입력을 소유하지 않는 플레이어 전투/행동 런타임입니다.
    public sealed class PlayerActor
    {
        private readonly PlayerCombo _combo;
        private readonly AttackWindow _attackWindow = new AttackWindow();
        private int _elapsed;
        private int _duration;
        private int _invulnerableRemaining;
        private int _facing = 1;
        private BossPoise _executionTarget;
        private int _bindPresses;

        public Player Definition { get; }
        public ActorStateMachine States { get; }
        public PlayerLocomotion Locomotion { get; }
        public PlayerStamina Stamina { get; }
        public int Health { get; private set; }
        public StateKey CurrentState => States.Current;
        // 현재 이 플레이어를 잡고 있는 일반몹. 구속(Bound) 중에만 값이 있습니다.
        public MonsterActor BoundBy { get; private set; }
        public int BindPressesRemaining => BoundBy == null ? 0 : _bindPresses;
        public bool IsDead => Health == 0;
        public bool IsAttacking => _combo.Index >= 0;
        public bool IsAttackActive => _attackWindow.IsActive;
        public AttackSpec CurrentAttack => _combo.Current;
        public bool IsInvulnerable => CurrentState == StateKey.Execute || _invulnerableRemaining > 0 ||
            (CurrentState == StateKey.Roll && _elapsed < Definition.RollIFrame);
        public event Action<int> HealthChanged;
        public event Action<int> AttackStarted;
        public event Action<bool> AttackWindowChanged
        {
            add { _attackWindow.Changed += value; }
            remove { _attackWindow.Changed -= value; }
        }
        public event Action<int, int, int> RollRequested;

        // attacks = 지상 1/2/3타, 공중 1타 순서. 초기 자원은 호출자가 명시합니다.
        public PlayerActor(Player definition, IReadOnlyList<AttackSpec> attacks, bool grounded, int currentStamina)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (definition.HP <= 0 || definition.RollTime < 0 || definition.RollIFrame < 0 || definition.RollIFrame > definition.RollTime ||
                definition.HitStunTime < 0 || definition.CritHitStunTime < 0 || definition.HitIFrame < 0 || definition.InputBuffer < 0)
                throw new ArgumentException("플레이어 HP·행동 시간 범위를 확인하세요.", nameof(definition));
            _combo = new PlayerCombo(attacks);
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
            if (CurrentState == StateKey.Bound)
            {
                // 구속 중 공격 입력은 연타 해제로만 소비합니다.
                if (--_bindPresses == 0) EndBind();
                return true;
            }
            if (IsAttacking)
            {
                return _combo.TryQueue(_elapsed, Definition.InputBuffer);
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
            BoundBy = null; _bindPresses = 0;
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

        // 일반몹 잡기. 공격 입력 presses번이면 풀립니다. duration>0이면 그 시간이 지나도 풀리고, 0이면 연타로만 풀립니다.
        public bool TryBind(MonsterActor by, int duration, int presses)
        {
            if (by == null) throw new ArgumentNullException(nameof(by));
            if (duration < 0 || presses <= 0) throw new ArgumentOutOfRangeException(nameof(duration));
            if (IsDead || IsInvulnerable || CurrentState == StateKey.Bound) return false;
            ClearAttack();
            BoundBy = by; _bindPresses = presses;
            _elapsed = 0; _duration = duration;
            States.Call(StateKey.Bound);
            return true;
        }

        // 잡은 몬스터의 사망·비활성화·피격 시 호출. 다른 몬스터의 구속은 풀지 않습니다.
        public bool ReleaseBind(MonsterActor by)
        {
            if (by == null || BoundBy != by || CurrentState != StateKey.Bound) return false;
            EndBind();
            return true;
        }

        // 판정 창마다 대상 ID를 한 번만 허용합니다. 충돌 위치/대상 선택은 외부에서 공급합니다.
        public bool TryRegisterTarget(int targetId) => _attackWindow.TryRegisterTarget(targetId);

        // 충돌 판정자가 공급한 대상에 보스 Dodge·플레이어 Crit 확률로 피해를 한 번 적용합니다. random은 이 플레이어 소유 난수입니다.
        public HitOutcome TryHitBoss(int targetId, BossPoise target, Func<int, int, int> random)
            => HitBoss(targetId, target, random ?? throw new ArgumentNullException(nameof(random)), false, false);

        // 검사·제어용: 회피/치명을 호출자가 확정합니다.
        public bool TryHitBoss(int targetId, BossPoise target, bool dodged, bool critical)
            => HitBoss(targetId, target, null, dodged, critical) >= HitOutcome.Hit;

        // 일반몹은 HP만 깎습니다. 지스타 일반몹 체세·해체 없음.
        public HitOutcome TryHitMonster(int targetId, MonsterActor target, Func<int, int, int> random)
            => HitMonster(targetId, target, random ?? throw new ArgumentNullException(nameof(random)), false, false);

        public bool TryHitMonster(int targetId, MonsterActor target, bool dodged, bool critical)
            => HitMonster(targetId, target, null, dodged, critical) >= HitOutcome.Hit;

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
                bool timed = IsAttacking || CurrentState == StateKey.Roll || CurrentState == StateKey.Hit || CurrentState == StateKey.CritHit ||
                    (CurrentState == StateKey.Bound && _duration > 0);
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
                        int next = _combo.Index + 1;
                        bool chain = _combo.Queued && _combo.Index < 2;
                        ClearAttack();
                        if (!chain || !StartAttack(next)) Locomotion.Resume();
                    }
                }
                else if (_elapsed == _duration)
                {
                    if (CurrentState == StateKey.Bound) EndBind();
                    else Locomotion.Resume();
                }
            }
        }

        private HitOutcome HitBoss(int targetId, BossPoise target, Func<int, int, int> random, bool dodged, bool critical)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            HitOutcome outcome = HitRoll.Resolve(_attackWindow, targetId, target.Definition.Dodge, Definition.Crit,
                target.IsDead || target.IsInvulnerable, true, random, dodged, critical);
            if (outcome < HitOutcome.Hit) return outcome;
            int damage = CombatMath.Damage(Definition, target.Definition, CurrentAttack.ExtraDamage, outcome == HitOutcome.Critical);
            return target.ReceiveHit(damage, CurrentAttack.PoiseDamage) ? outcome : HitOutcome.None;
        }

        private HitOutcome HitMonster(int targetId, MonsterActor target, Func<int, int, int> random, bool dodged, bool critical)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            HitOutcome outcome = HitRoll.Resolve(_attackWindow, targetId, target.Definition.Dodge, Definition.Crit,
                target.IsDead, true, random, dodged, critical);
            if (outcome < HitOutcome.Hit) return outcome;
            bool isCritical = outcome == HitOutcome.Critical;
            int damage = CombatMath.Damage(Definition, target.Definition, CurrentAttack.ExtraDamage, isCritical);
            return target.ReceiveHit(damage, isCritical) ? outcome : HitOutcome.None;
        }

        private void EndBind()
        {
            BoundBy = null; _bindPresses = 0;
            Locomotion.Resume();
        }

        private void AdvanceTime(int milliseconds)
        {
            Stamina.Tick(milliseconds);
            _invulnerableRemaining = Math.Max(0, _invulnerableRemaining - milliseconds);
        }

        private bool StartAttack(int index)
        {
            if (!Stamina.TryConsume(_combo.GetAttack(index).StaminaCost)) return false;
            _combo.Start(index); _elapsed = 0; _attackWindow.ClearTargets();
            States.Change(new AttackState(index == 3 ? StateKey.Attack1 : (index == 0 ? StateKey.Attack1 : index == 1 ? StateKey.Attack2 : StateKey.Attack3)));
            AttackStarted?.Invoke(index);
            return true;
        }

        private void ClearAttack()
        {
            SetAttackActive(false);
            _combo.Clear(); _attackWindow.ClearTargets();
        }

        private void SetAttackActive(bool active) => _attackWindow.SetActive(active);
    }
}
