using System;
using System.Collections.Generic;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay.States;

namespace HumanGhost.Gameplay
{
    // 위치·충돌·시간·난수를 직접 읽지 않는 일반몹 AI 런타임. 관측과 정수 ms를 받아 상태·이동 요청을 결정합니다.
    // 지스타 일반몹은 체세·해체 없음(10/30 확정).
    public sealed class MonsterActor
    {
        private readonly IdleState _idle = new IdleState();
        private readonly WalkState _walk = new WalkState();
        private readonly MonsterPattern[] _patterns;
        private readonly List<MonsterPattern> _candidates = new List<MonsterPattern>();
        private readonly AttackWindow _attackWindow = new AttackWindow();
        private readonly Func<int, int, int> _random;
        private readonly int _hitStun;
        private readonly int _critHitStun;
        private MonsterPattern _pattern;
        private PlayerActor _holding;
        private int _elapsed;
        private int _stunRemaining;
        private int _waitRemaining;
        private int _targetId;
        private int _distance;
        private int _direction;

        public Monster Definition { get; }
        public ActorStateMachine States { get; }
        public int Health { get; private set; }
        public StateKey CurrentState => States.Current;
        public bool IsDead => Health == 0;
        public int Facing { get; private set; } = 1;
        public MonsterPattern CurrentPattern => _pattern;
        public bool IsAttackActive => _attackWindow.IsActive;
        public bool IsCharging => IsAttackActive && _pattern.IsCharge;
        // SuperArmor는 돌진 판정 구간에만 적용합니다. 상시 피격 면제가 아닙니다.
        public bool IsSuperArmor => IsCharging && Definition.SuperArmor;
        // tile_x100/초 그대로. 월드 단위 변환·실제 이동은 어댑터 책임입니다.
        public int Velocity => IsCharging ? Facing * _pattern.ChargeSpeed : CurrentState == StateKey.Walk ? Facing * Definition.MoveSpeed : 0;
        public event Action<int> HealthChanged;
        public event Action<MonsterPattern> PatternStarted;
        // 공격 취소와 경직 진입을 완료한 뒤 표시 소비자에게 알립니다.
        public event Action Parried;
        public event Action<bool> AttackWindowChanged
        {
            add { _attackWindow.Changed += value; }
            remove { _attackWindow.Changed -= value; }
        }

        // random(minInclusive, maxExclusive): 패턴 선택·대기 시간용 개체별 난수. 전역 난수를 쓰지 않습니다.
        public MonsterActor(Monster definition, IReadOnlyList<MonsterPattern> patterns, int hitStun, int critHitStun, Func<int, int, int> random)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (definition.HP <= 0 || definition.MoveSpeed < 0 || definition.SightRange < 0 || definition.AttackRange < 0 ||
                definition.IdleMin < 0 || definition.IdleMax < definition.IdleMin)
                throw new ArgumentException("일반몹 HP·속도·거리·대기 범위를 확인하세요.", nameof(definition));
            if (patterns == null || patterns.Count == 0) throw new ArgumentException("패턴 설정이 필요합니다.", nameof(patterns));
            if (hitStun < 0 || critHitStun < 0) throw new ArgumentOutOfRangeException(nameof(hitStun));
            _patterns = new MonsterPattern[patterns.Count];
            for (int i = 0; i < _patterns.Length; i++) _patterns[i] = patterns[i] ?? throw new ArgumentException("빈 패턴 설정입니다.", nameof(patterns));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _hitStun = hitStun; _critHitStun = critHitStun;
            States = new ActorStateMachine(_idle);
            Health = definition.HP;
        }

        // 외부 관측: 목표 개체ID(양수)·거리(tile_x100)·목표 방향(-1/0/1). 목표 사망/이탈 시 ClearTarget.
        public void Observe(int targetId, int distance, int direction)
        {
            if (targetId <= 0) throw new ArgumentOutOfRangeException(nameof(targetId));
            if (distance < 0) throw new ArgumentOutOfRangeException(nameof(distance));
            if (direction < -1 || direction > 1) throw new ArgumentOutOfRangeException(nameof(direction));
            _targetId = targetId; _distance = distance; _direction = direction;
            Decide();
        }

        // 잡고 있던 목표를 잃으면 구속을 풀고 후딜로 진행합니다. 일반 패턴은 끝까지 진행합니다.
        public void ClearTarget()
        {
            _targetId = 0;
            if (_holding != null) { _holding.ReleaseBind(this); _holding = null; }
            Decide();
        }

        // 물리 어댑터의 벽 충돌 관측. 돌진 판정 중에만 허점(Stun)으로 전환합니다.
        public bool NotifyWallHit()
        {
            if (!IsCharging) return false;
            int stun = _pattern.WallStun;
            ClearPattern();
            if (stun == 0) { StartWait(); return true; }
            _stunRemaining = stun;
            States.Call(StateKey.Stun);
            return true;
        }

        // 외부 판정이 확정한 HP 피해. 회피·방어 계산은 CombatMath/판정자 책임입니다.
        public bool ReceiveHit(int damage, bool critical = false)
        {
            if (damage < 0) throw new ArgumentOutOfRangeException(nameof(damage));
            if (IsDead || damage == 0) return false;
            Health = Math.Max(0, Health - damage);
            if (IsDead)
            {
                ClearPattern(); _stunRemaining = 0; _waitRemaining = 0;
                States.Change(new DeadState());
            }
            // 벽 충돌 허점(Stun)은 피해만 받고 남은 허점 시간을 유지합니다.
            else if (!IsSuperArmor && CurrentState != StateKey.Stun)
            {
                ClearPattern(); _waitRemaining = 0;
                _stunRemaining = critical ? _critHitStun : _hitStun;
                States.Change(new HitState(critical));
                if (_stunRemaining == 0) EnterNeutral();
            }
            HealthChanged?.Invoke(Health);
            return true;
        }

        // 판정자가 확정한 패링을 적용합니다. 표시 구독 없이도 전투 상태를 정리합니다.
        public bool ReceiveParried(int stunMilliseconds)
        {
            if (stunMilliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(stunMilliseconds));
            if (IsDead || !IsAttackActive || _pattern == null || _pattern.IsGrab) return false;
            ClearPattern(); _waitRemaining = 0;
            _stunRemaining = stunMilliseconds;
            States.Call(StateKey.Stun);
            Parried?.Invoke();
            return true;
        }

        // 판정 창마다 대상 ID를 한 번만 허용합니다.
        public bool TryRegisterTarget(int targetId) => _attackWindow.TryRegisterTarget(targetId);

        // 충돌 판정자가 공급한 대상에 플레이어 Dodge·이 몬스터 Crit 확률로 한 번 적용. 잡기는 비치명 피해와 구속을 함께 적용하며 회피만 판정합니다.
        // random은 패턴/대기용과 분리된 이 몬스터 소유 타격 난수입니다.
        public HitOutcome TryHitPlayer(int targetId, PlayerActor target, Func<int, int, int> random, int direction = 0)
            => HitPlayer(targetId, target, random ?? throw new ArgumentNullException(nameof(random)), false, false, direction);

        // 검사·제어용: 회피/치명을 호출자가 확정합니다.
        public bool TryHitPlayer(int targetId, PlayerActor target, bool dodged, bool critical)
            => HitPlayer(targetId, target, null, dodged, critical) >= HitOutcome.Hit;

        private HitOutcome HitPlayer(int targetId, PlayerActor target, Func<int, int, int> random, bool dodged, bool critical, int direction = 0)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            bool grab = _pattern != null && _pattern.IsGrab;
            if (!IsDead && !grab && target.TryParryContact(_attackWindow, targetId, direction))
            {
                ReceiveParried(target.ParryStunTime);
                return HitOutcome.Parried;
            }
            HitOutcome outcome = HitRoll.Resolve(_attackWindow, targetId, target.CombatStats.Dodge, Definition.Crit,
                target.IsDead || target.IsInvulnerable, !grab, random, dodged, critical);
            if (outcome < HitOutcome.Hit) return outcome;
            bool isCritical = outcome == HitOutcome.Critical;
            int damage = CombatMath.Damage(Definition, target.CombatStats, _pattern.Attack.ExtraDamage, isCritical);
            if (!grab)
            {
                if (!target.ReceiveHit(damage, isCritical)) return HitOutcome.None;
                if (_pattern.IsCharge)
                {
                    _elapsed = _pattern.Attack.Windup + _pattern.Attack.Active;
                    SetAttackActive(false);
                }
                return outcome;
            }
            if (!target.TryBind(this, _pattern.BindTime, _pattern.BindPresses, damage)) return HitOutcome.None;
            // 잡는 동안 패턴 시간을 멈추고, 플레이어가 풀려나면 후딜로 이어갑니다. 사망 대상은 잡지 않습니다.
            _holding = target.IsDead ? null : target;
            _elapsed = _pattern.Attack.Windup + _pattern.Attack.Active;
            SetAttackActive(false);
            return outcome;
        }

        // 비활성화·풀 반환 시 남은 구속·판정·대기를 정리합니다. HP는 유지합니다.
        public void Release()
        {
            ClearPattern(); _stunRemaining = 0; _waitRemaining = 0; _targetId = 0;
            if (!IsDead) States.Change(_idle);
        }

        public void Tick(int milliseconds)
        {
            if (milliseconds < 0) throw new ArgumentOutOfRangeException(nameof(milliseconds));
            while (milliseconds > 0 && !IsDead)
            {
                if (_holding != null)
                {
                    if (_holding.BoundBy == this) return;
                    _holding = null;
                }
                if (_pattern != null)
                {
                    AttackSpec spec = _pattern.Attack;
                    int end = _elapsed < spec.Windup ? spec.Windup : _elapsed < spec.Windup + spec.Active ? spec.Windup + spec.Active : spec.TotalTime;
                    int step = Math.Min(milliseconds, end - _elapsed);
                    _elapsed += step; milliseconds -= step;
                    SetAttackActive(_elapsed >= spec.Windup && _elapsed < spec.Windup + spec.Active);
                    if (_elapsed == spec.TotalTime) { ClearPattern(); StartWait(); }
                }
                else if (_stunRemaining > 0)
                {
                    int step = Math.Min(milliseconds, _stunRemaining);
                    _stunRemaining -= step; milliseconds -= step;
                    if (_stunRemaining > 0) continue;
                    if (CurrentState == StateKey.Stun) StartWait();
                    else EnterNeutral();
                }
                else if (_waitRemaining > 0)
                {
                    int step = Math.Min(milliseconds, _waitRemaining);
                    _waitRemaining -= step; milliseconds -= step;
                    if (_waitRemaining == 0) EnterNeutral();
                }
                else break;
            }
        }

        // 배회(목표 없음 → 대기) → 발견(시야 안 → 추적) → 공격 거리 안이면 거리가 맞는 패턴 중 하나.
        // shortcut: 배회 이동 범위가 시트/기획에 없어 목표가 없으면 제자리 대기, 순찰 기획 확정 시 추가.
        private void Decide()
        {
            if (IsDead || _pattern != null || _stunRemaining > 0 || _waitRemaining > 0) return;
            if (_targetId == 0 || _distance > Definition.SightRange) { States.Change(_idle); return; }
            if (_direction != 0) Facing = _direction;
            if (_distance <= Definition.AttackRange)
            {
                _candidates.Clear();
                foreach (MonsterPattern pattern in _patterns) if (_distance <= pattern.Range && _distance >= pattern.MinRange) _candidates.Add(pattern);
                if (_candidates.Count > 0) { StartPattern(_candidates[Pick(0, _candidates.Count)]); return; }
            }
            States.Change(_walk);
        }

        private void StartPattern(MonsterPattern pattern)
        {
            _pattern = pattern; _elapsed = 0; _attackWindow.ClearTargets();
            if (pattern.Key == StateKey.Skill1) States.Call(StateKey.Skill1);
            else States.Change(new AttackState(pattern.Key));
            PatternStarted?.Invoke(pattern);
        }

        // 패턴 사이 대기 IdleMin~IdleMax(양끝 포함).
        private void StartWait()
        {
            _waitRemaining = Pick(Definition.IdleMin, checked(Definition.IdleMax + 1));
            States.Change(_idle);
            if (_waitRemaining == 0) Decide();
        }

        private void EnterNeutral()
        {
            States.Change(_idle);
            Decide();
        }

        private int Pick(int min, int maxExclusive)
        {
            int value = _random(min, maxExclusive);
            if (value < min || value >= maxExclusive) throw new InvalidOperationException("난수 공급 범위를 벗어났습니다.");
            return value;
        }

        private void ClearPattern()
        {
            SetAttackActive(false);
            _pattern = null; _attackWindow.ClearTargets();
            if (_holding != null) { _holding.ReleaseBind(this); _holding = null; }
        }

        private void SetAttackActive(bool active) => _attackWindow.SetActive(active);
    }
}
