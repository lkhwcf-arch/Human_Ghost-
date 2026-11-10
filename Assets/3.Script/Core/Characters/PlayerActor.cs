using System;
using System.Collections.Generic;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay.States;

namespace HumanGhost.Gameplay
{
    // 위치·히트박스·입력을 소유하지 않는 플레이어 전투/행동 런타임입니다.
    public sealed partial class PlayerActor
    {
        private readonly PlayerCombo _combo;
        private readonly AttackWindow _attackWindow = new AttackWindow();
        private int _elapsed;
        private int _duration;
        private int _invulnerableRemaining;
        private int _facing = 1;
        private BossPoise _executionTarget;
        private int _bindPresses;
        private bool _cutsceneDamageBlocked;
        private PlayerSkill _skill;
        private PlayerHerbs _herbs;
        private bool _usingSkill;

        public Player Definition { get; }
        public ActorStateMachine States { get; }
        public PlayerLocomotion Locomotion { get; }
        public PlayerStamina Stamina { get; }
        public int Health { get; private set; }
        public StateKey CurrentState => States.Current;
        // 현재 이 플레이어를 잡고 있는 일반몹. 구속(Bound) 중에만 값이 있습니다.
        public MonsterActor BoundBy { get; private set; }
        public int BindPressesRemaining => CurrentState == StateKey.Bound ? _bindPresses : 0;
        // 보스 사슬 구속입니다. 일반몹 구속과 달리 잡은 일반몹이 없습니다.
        public bool IsChained => CurrentState == StateKey.Bound && BoundBy == null;
        public bool IsHanging => CurrentState == StateKey.Hang;
        public bool IsDead => Health == 0;
        public bool IsAttacking => _combo.Index >= 0;
        public bool IsAttackActive => _attackWindow.IsActive;
        public AttackSpec CurrentAttack => _usingSkill ? _skill.Attack : _combo.Current;
        // 결박 가르기(C07) 해금·연결 여부와 남은 쿨타임(ms)입니다.
        public bool HasSkill => _skill != null;
        public int SkillCooldownRemaining => _skill == null ? 0 : _skill.Remaining;
        public int SkillCooldownSeconds => _skill == null ? 0 : _skill.CooldownSeconds;
        public event Action DisplayChanged;
        public int HerbCount => _herbs == null ? 0 : _herbs.Count;
        public bool HasHerbs => _herbs != null;
        // 약초 사용 중 이동 속도(tile_x100/초, 부호 포함). C04의 ‘느려짐’ 비율은 개발 값 절반입니다.
        public int HealMoveSpeed => CurrentState == StateKey.Heal ? Locomotion.Direction * Definition.MoveSpeed / 2 : 0;
        public bool IsInvulnerable => _cutsceneDamageBlocked || CurrentState == StateKey.Execute || _invulnerableRemaining > 0 ||
            (CurrentState == StateKey.Roll && _elapsed < Definition.RollIFrame);
        public event Action<int> HealthChanged;
        public event Action<int> AttackStarted;
        public event Action<bool> AttackWindowChanged
        {
            add { _attackWindow.Changed += value; }
            remove { _attackWindow.Changed -= value; }
        }
        public event Action<int, int, int> RollRequested;
        // 스태미나가 모자라 공격·구르기를 못 했을 때입니다(C03 게이지 깜빡임).
        public event Action StaminaRefused;
        // 방향·거리(tile_x100)·이동 시간(ms). 위치 이동은 외부가 합니다.
        public event Action<int, int, int> SkillStarted;

        // attacks = 지상 1/2/3/4타, 공중 1타 순서. 초기 자원은 호출자가 명시합니다.
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
            States.Changed += OnDisplayValueChanged;
            Stamina.Changed += OnDisplayValueChanged;
            HealthChanged += OnDisplayValueChanged;
        }

        public void SetMove(int direction)
        {
            Locomotion.SetMove(direction);
            if (direction != 0 && !IsActionBusy) _facing = direction;
        }

        public bool TryJump() => !IsDead && Locomotion.TryJump();

        // 공중에서 외부가 모서리를 확인한 뒤 요청합니다. 매달린 동안 공격·구르기·이동은 막히고 스태미나를 쓰지 않습니다.
        public bool TryHang()
        {
            if (IsDead || !Locomotion.CanMove || Locomotion.IsGrounded) return false;
            States.Call(StateKey.Hang);
            return true;
        }

        // 올라가기는 접지 관측을 먼저 전달한 뒤 호출합니다. 놓기는 공중 상태로 돌아갑니다.
        public bool EndHang()
        {
            if (!IsHanging) return false;
            Locomotion.Resume();
            return true;
        }

        // 해금된 뒤 조립자가 한 번 연결합니다. 미연결이면 입력해도 나가지 않습니다.
        public void ConfigureSkill(AttackSpec attack, int cooldown, int distance)
        {
            var skill = new PlayerSkill(attack, cooldown, distance);
            if (_skill != null) _skill.CooldownSecondsChanged -= OnDisplayValueChanged;
            _skill = skill;
            _skill.CooldownSecondsChanged += OnDisplayValueChanged;
            NotifyDisplayChanged();
        }

        // 조립자가 Buff 값으로 한 번 연결합니다. 재시작은 새 개체로 다시 채웁니다.
        public void ConfigureHerbs(int heal, int useTime, int charges)
        {
            var herbs = new PlayerHerbs(heal, useTime, charges);
            if (_herbs != null) _herbs.Changed -= NotifyDisplayChanged;
            _herbs = herbs;
            _herbs.Changed += NotifyDisplayChanged;
            NotifyDisplayChanged();
        }
        private void NotifyDisplayChanged() => DisplayChanged?.Invoke();
        private void OnDisplayValueChanged(int value) => NotifyDisplayChanged();
        private void OnDisplayValueChanged(StateKey value) => NotifyDisplayChanged();

        // 맵 이동처럼 같은 진행을 이어 갈 때 HP·약초를 되돌립니다. 살아 있는 값만 받습니다. 약초 연결 뒤 호출합니다.
        public void RestoreVitals(int health, int herbs)
        {
            if (health <= 0 || health > Definition.HP) throw new ArgumentOutOfRangeException(nameof(health));
            if (_herbs == null) throw new InvalidOperationException("약초 연결이 필요합니다.");
            _herbs.Restore(herbs);
            Health = health;
            HealthChanged?.Invoke(Health);
        }

        // 사용 시간 뒤 회복합니다. 그 사이 맞으면 취소되고 개수는 줄어든 채입니다. 체력이 가득하면 쓰지 않습니다.
        public bool TryUseHerb()
        {
            if (_herbs == null || IsDead || !Locomotion.CanMove || Health >= Definition.HP || !_herbs.TryConsume()) return false;
            _elapsed = 0; _duration = _herbs.UseTime;
            States.Call(StateKey.Heal);
            if (_duration == 0) FinishHerb();
            return true;
        }

        private void FinishHerb()
        {
            Health = Math.Min(Definition.HP, Health + _herbs.Heal);
            HealthChanged?.Invoke(Health);
            Locomotion.Resume();
        }

        // 스태미나를 쓰지 않습니다. 보스 사슬에 잡힌 동안에도 쓸 수 있고 사슬을 끊습니다. 매달리기·일반몹 구속 중에는 쓸 수 없습니다.
        public bool TryUseSkill()
        {
            if (_skill == null || IsDead || !_skill.IsReady || !(Locomotion.CanMove || IsChained)) return false;
            if (IsChained) EndBind();
            ClearAttack();
            _usingSkill = true; _elapsed = 0;
            _skill.StartCooldown();
            States.Call(StateKey.Skill1);
            SkillStarted?.Invoke(_facing, _skill.Distance, _skill.Attack.Windup + _skill.Attack.Active);
            return true;
        }

        // 접지 관측을 먼저 전달한 뒤 낙하 높이(tile_x100)와 함께 호출합니다. 낙하 피해는 없고 조건을 넘으면 착지 모션만 재생합니다.
        public bool NotifyLanded(int fallHeight)
        {
            if (IsDead || !Locomotion.CanMove || Definition.LandTime <= 0 || fallHeight < Definition.LandFallHeight) return false;
            _elapsed = 0; _duration = Definition.LandTime;
            States.Call(StateKey.Land);
            return true;
        }

        // 구덩이 낙하(K04). 무적과 상관없이 고정 피해를 받으며 true면 낙사입니다. 컷씬 차단 중에는 받지 않습니다.
        public bool ApplyFall(int damage)
        {
            if (damage < 0) throw new ArgumentOutOfRangeException(nameof(damage));
            if (IsDead || _cutsceneDamageBlocked) return IsDead;
            Health = Math.Max(0, Health - damage);
            ClearAttack();
            BoundBy = null; _bindPresses = 0;
            if (IsDead) States.Change(new DeadState());
            HealthChanged?.Invoke(Health);
            return IsDead;
        }

        // 메뉴 전환은 다음 타수 선입력만 지우며 현재 공격은 유지합니다.
        public void DiscardBufferedInput() => _combo.DiscardBufferedInput();

        public bool TryAttack()
        {
            if (IsDead) return false;
            if (CurrentState == StateKey.Bound)
            {
                // 구속 중 공격 입력은 연타 해제로만 소비합니다.
                if (--_bindPresses == 0) EndBind();
                else NotifyDisplayChanged();
                return true;
            }
            if (IsAttacking)
            {
                return _combo.TryQueue(_elapsed, Definition.InputBuffer);
            }
            if (!Locomotion.CanMove) return false;
            return StartAttack(Locomotion.IsGrounded ? 0 : 4);
        }

        public bool TryRoll()
        {
            if (IsDead || CurrentState == StateKey.Attack4 || (IsAttacking ? _elapsed < CurrentAttack.Windup + CurrentAttack.Active : !Locomotion.CanMove)) return false;
            if (!Stamina.TryConsumeRoll()) { StaminaRefused?.Invoke(); return false; }
            ClearAttack();
            _elapsed = 0; _duration = Definition.RollTime;
            States.Call(StateKey.Roll);
            RollRequested?.Invoke(_facing, Definition.RollDistance, Definition.RollTime);
            if (_duration == 0) Locomotion.Resume();
            return true;
        }

        // 컷씬 조립자 한 곳이 시작 전 켜고 완료·스킵·취소·실패 때 해제합니다. 입력/시간 잠금은 별도입니다.
        public void SetCutsceneDamageBlocked(bool blocked) => _cutsceneDamageBlocked = blocked;

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
        public bool TryBind(MonsterActor by, int duration, int presses, int damage = 0)
        {
            if (by == null) throw new ArgumentNullException(nameof(by));
            return Bind(by, duration, presses, damage);
        }

        // 보스 사슬 구속. 일반몹 잡기와 같은 연타 해제·상한 시간을 씁니다.
        public bool TryChainBind(int duration, int presses, int damage = 0) => Bind(null, duration, presses, damage);

        // 사슬 패턴이 끝나거나 비활성화될 때 호출합니다.
        public bool ReleaseChain()
        {
            if (!IsChained) return false;
            EndBind();
            return true;
        }

        private bool Bind(MonsterActor by, int duration, int presses, int damage)
        {
            if (duration < 0 || presses <= 0 || damage < 0) throw new ArgumentOutOfRangeException(nameof(duration));
            if (IsDead || IsInvulnerable || CurrentState == StateKey.Bound) return false;
            ClearAttack();
            Health = Math.Max(0, Health - damage);
            if (damage > 0) _invulnerableRemaining = Definition.HitIFrame;
            if (IsDead)
            {
                States.Change(new DeadState());
                HealthChanged?.Invoke(Health);
                return true;
            }
            BoundBy = by; _bindPresses = presses;
            _elapsed = 0; _duration = duration;
            States.Call(StateKey.Bound);
            if (damage > 0) HealthChanged?.Invoke(Health);
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
            TickAction(ref milliseconds);
            if (IsActionBusy) return;
            while (milliseconds > 0)
            {
                bool timed = IsAttacking || _usingSkill || CurrentState == StateKey.Heal || CurrentState == StateKey.Land || CurrentState == StateKey.Roll || CurrentState == StateKey.Hit || CurrentState == StateKey.CritHit ||
                    (CurrentState == StateKey.Bound && _duration > 0);
                if (!timed) { AdvanceTime(milliseconds); break; }
                AttackSpec spec = CurrentAttack;
                int end = IsAttacking || _usingSkill ? (_elapsed < spec.Windup ? spec.Windup : _elapsed < spec.Windup + spec.Active ? spec.Windup + spec.Active : spec.TotalTime) : _duration;
                int step = Math.Min(milliseconds, end - _elapsed);
                AdvanceTime(step);
                _elapsed += step; milliseconds -= step;
                if (_usingSkill)
                {
                    if (_elapsed == spec.Windup) SetAttackActive(true);
                    if (_elapsed == spec.Windup + spec.Active) SetAttackActive(false);
                    if (_elapsed == spec.TotalTime) { ClearAttack(); Locomotion.Resume(); }
                }
                else if (IsAttacking)
                {
                    if (_elapsed == spec.Windup) SetAttackActive(true);
                    if (_elapsed == spec.Windup + spec.Active) SetAttackActive(false);
                    if (_elapsed == spec.TotalTime)
                    {
                        int next = _combo.Index + 1;
                        bool chain = _combo.Queued && _combo.Index < 3;
                        ClearAttack();
                        if (!chain || !StartAttack(next)) Locomotion.Resume();
                    }
                }
                else if (_elapsed == _duration)
                {
                    if (CurrentState == StateKey.Bound) EndBind();
                    else if (CurrentState == StateKey.Heal) FinishHerb();
                    else Locomotion.Resume();
                }
            }
        }

        private HitOutcome HitBoss(int targetId, BossPoise target, Func<int, int, int> random, bool dodged, bool critical)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            // 결박 가르기는 회피 확률을 굴리지 않습니다(C07).
            HitOutcome outcome = HitRoll.Resolve(_attackWindow, targetId, _usingSkill ? 0 : target.Definition.Dodge, Definition.Crit,
                target.IsDead || target.IsInvulnerable, true, random, !_usingSkill && dodged, critical);
            if (outcome < HitOutcome.Hit) return outcome;
            int damage = CombatMath.Damage(Definition, target.Definition, CurrentAttack.ExtraDamage, outcome == HitOutcome.Critical);
            return target.ReceiveHit(damage, CurrentAttack.PoiseDamage) ? outcome : HitOutcome.None;
        }

        private HitOutcome HitMonster(int targetId, MonsterActor target, Func<int, int, int> random, bool dodged, bool critical)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            HitOutcome outcome = HitRoll.Resolve(_attackWindow, targetId, _usingSkill ? 0 : target.Definition.Dodge, Definition.Crit,
                target.IsDead, true, random, !_usingSkill && dodged, critical);
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
            _skill?.Tick(milliseconds);
        }

        private bool StartAttack(int index)
        {
            if (!Stamina.TryConsume(_combo.GetAttack(index).StaminaCost)) { StaminaRefused?.Invoke(); return false; }
            _combo.Start(index); _elapsed = 0; _attackWindow.ClearTargets();
            States.Change(new AttackState(index == 4 ? StateKey.Attack1 : index == 0 ? StateKey.Attack1 : index == 1 ? StateKey.Attack2 : index == 2 ? StateKey.Attack3 : StateKey.Attack4));
            AttackStarted?.Invoke(index);
            return true;
        }

        private void ClearAttack()
        {
            CancelCombatAction();
            SetAttackActive(false);
            _combo.Clear(); _attackWindow.ClearTargets();
            _usingSkill = false;
        }

        private void SetAttackActive(bool active) => _attackWindow.SetActive(active);
    }
}
