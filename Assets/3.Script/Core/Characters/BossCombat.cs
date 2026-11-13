using System;
using System.Collections.Generic;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay.States;

namespace HumanGhost.Gameplay
{
    // 보스 기본 패턴의 실행·판정 창·패턴 간 대기만 소유합니다. HP·체세·노출·해체와 상태 머신은 BossPoise 소유이며 같은 States를 씁니다.
    // 선택·접촉 관측은 외부 책임입니다. BossPoise가 전환/노출/처형을 시작하면 즉시 패턴을 정리합니다. 시간은 각각 한 번 공급합니다.
    public sealed class BossCombat
    {
        private readonly BossPattern[] _patterns;
        private readonly AttackWindow _attackWindow = new AttackWindow();
        private readonly Func<int, int, int> _random;
        private BossPattern _pattern;
        private int _hit;
        private int _elapsed;
        private int _waitRemaining;
        private PlayerActor _chained;
        private readonly HashSet<int> _groupTargets = new HashSet<int>();
        private int _targetDirection = 1;
        private bool _airborne;
        public event Action<bool> AirborneChanged;
        private void UpdateAirborne()
        {
            bool value = IsAirborne;
            Boss.SetPatternInvulnerable(value);
            if (_airborne == value) return;
            _airborne = value; AirborneChanged?.Invoke(value);
        }
        public int PatternElapsed
        {
            get { int value = _elapsed; if (_pattern != null) for (int i = 0; i < _hit; i++) value += _pattern.Hits[i].TotalTime; return value; }
        }
        public bool IsAirborne => _pattern?.Kind == BossPatternKind.DesperationLeap && _hit % 2 == 0 &&
            _elapsed >= (_hit == 0 ? _pattern.Hits[0].Windup - 900 : 0) && _elapsed < _pattern.Hits[_hit].Windup;
        public bool IsWindingUp => _pattern != null && _elapsed < _pattern.Hits[_hit].Windup;

        public BossPoise Boss { get; }
        public int PatternCount => _patterns.Length;
        public bool HasPhaseTwoPatterns { get; }
        public BossPattern CurrentPattern => _pattern;
        // 현재 패턴 안 타격 순번(0부터). 패턴이 없으면 -1.
        public int HitIndex => _pattern == null ? -1 : _hit;
        public int HitElapsed => _pattern == null ? 0 : _elapsed;
        public bool IsAttacking => _pattern != null;
        public bool IsAttackActive => _attackWindow.IsActive;
        public bool IsWaiting => _waitRemaining > 0;
        public BossHitReaction CurrentReaction => _pattern != null && _pattern.Reactions.Count > 0 ? _pattern.Reactions[_hit] : null;
        // 외부 관측이 정한 바라보는 방향(-1/1). 패턴 중에는 고정됩니다.
        public int Facing { get; private set; } = 1;
        public bool IsCharging => _pattern != null && _pattern.IsCharge && _attackWindow.IsActive;
        // 어댑터가 매 프레임 적용할 이동 속도(tile_x100/초, 부호 포함). 돌진 중에만 0이 아닙니다.
        public int Velocity => IsCharging ? Facing * _pattern.ChargeSpeed : 0;
        // 사슬에 잡힌 플레이어를 끄는 속도(tile_x100/초, 부호 포함). 잡고 있을 때만 0이 아닙니다.
        public int PullVelocity => _chained != null && _chained.IsChained ? -Facing * _pattern.PullSpeed : 0;
        public BossPattern GetPattern(int index) => _patterns[index];
        public event Action<BossPattern> PatternStarted;
        public event Action<bool> AttackWindowChanged
        {
            add { _attackWindow.Changed += value; }
            remove { _attackWindow.Changed -= value; }
        }

        // random(minInclusive, maxExclusive): 패턴 간 대기용 개체별 난수. 타격 확률 난수와 분리합니다.
        public BossCombat(BossPoise boss, IReadOnlyList<BossPattern> patterns, Func<int, int, int> random)
        {
            Boss = boss ?? throw new ArgumentNullException(nameof(boss));
            if (boss.Phase.IdleMin < 0 || boss.Phase.IdleMax < boss.Phase.IdleMin) throw new ArgumentException("보스 대기 범위를 확인하세요.", nameof(boss));
            if (patterns == null || patterns.Count == 0) throw new ArgumentException("보스 패턴 설정이 필요합니다.", nameof(patterns));
            _patterns = new BossPattern[patterns.Count];
            for (int i = 0; i < _patterns.Length; i++) _patterns[i] = patterns[i] ?? throw new ArgumentException("빈 보스 패턴입니다.", nameof(patterns));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            HasPhaseTwoPatterns = Array.Exists(_patterns, pattern => pattern.Kind != BossPatternKind.Legacy);
            // 사망·노출·해체는 패턴보다 우선. 같은 호출 안에서 창·대기를 정리해 다음 Tick 전 타격도 막습니다.
            Boss.Changed += OnBossChanged;
        }

        public void Face(int direction)
        {
            if (direction != -1 && direction != 1) throw new ArgumentOutOfRangeException(nameof(direction));
            _targetDirection = direction;
            if (_pattern == null) Facing = direction;
        }

        // 외부 선택 요청. 사망·노출·해체·실행 중·대기 중·현재 페이즈 이전 패턴이면 거부합니다.
        public bool TryStart(int index)
        {
            if (index < 0 || index >= _patterns.Length) throw new ArgumentOutOfRangeException(nameof(index));
            if (IsBlocked || _pattern != null || _waitRemaining > 0 || (Boss.Phase.Phase < _patterns[index].MinPhase || Boss.Phase.Phase > _patterns[index].MaxPhase)) return false;
            if (_patterns[index].Kind == BossPatternKind.DesperationLeap && (long)Boss.Health * 4 > Boss.Definition.HP) return false;
            _pattern = _patterns[index]; _hit = 0; _elapsed = 0;
            _attackWindow.ClearTargets(); _groupTargets.Clear();
            if (_pattern.Key == StateKey.Skill1) Boss.States.Call(StateKey.Skill1);
            else Boss.States.Change(new AttackState(_pattern.Key));
            PatternStarted?.Invoke(_pattern);
            return true;
        }

        // 충돌 판정자가 공급한 접촉에 플레이어 Dodge·보스 Crit 확률로 한 번 적용. random은 이 보스 소유 타격 난수입니다.
        public HitOutcome TryHitPlayer(int targetId, PlayerActor target, Func<int, int, int> random, int direction = 0)
            => HitPlayer(targetId, target, random ?? throw new ArgumentNullException(nameof(random)), false, false, direction);

        // 검사·제어용: 회피/치명을 호출자가 확정합니다.
        public bool TryHitPlayer(int targetId, PlayerActor target, bool dodged, bool critical)
            => HitPlayer(targetId, target, null, dodged, critical) >= HitOutcome.Hit;

        public void Tick(int milliseconds)
        {
            if (milliseconds < 0) throw new ArgumentOutOfRangeException(nameof(milliseconds));
            while (milliseconds > 0 && !IsBlocked)
            {
                if (_pattern != null)
                {
                    AttackSpec spec = _pattern.Hits[_hit];
                    // 2페이즈 돌진은 시간 상한 대신 실제 벽에 닿을 때까지 판정을 유지합니다.
                    if (_pattern.Kind == BossPatternKind.CrawlCharge && _attackWindow.IsActive)
                    { milliseconds = 0; break; }
                    int end = _elapsed < spec.Windup ? spec.Windup : _elapsed < spec.Windup + spec.Active ? spec.Windup + spec.Active : spec.TotalTime;
                    if (_pattern.Kind == BossPatternKind.DesperationLeap && _hit == 0 && _elapsed < spec.Windup - 900)
                        end = Math.Min(end, spec.Windup - 900);
                    int step = Math.Min(milliseconds, end - _elapsed);
                    _elapsed += step; milliseconds -= step;
                    UpdateAirborne();
                    _attackWindow.SetActive(_elapsed >= spec.Windup && _elapsed < spec.Windup + spec.Active);
                    // 창 콜백(동기 접촉)이 벽 충돌 노출·Release로 패턴을 끝냈으면 끝난 패턴을 진행하지 않습니다.
                    if (_pattern == null) break;
                    if (_elapsed < spec.TotalTime) continue;
                    // 다음 타는 새 판정 창: 같은 대상을 다시 한 번 허용합니다.
                    if (++_hit < _pattern.Hits.Count)
                    {
                        if (_pattern.Areas.Count == 0 || _pattern.Areas[_hit].DamageGroup == 0 ||
                            _pattern.Areas[_hit].DamageGroup != _pattern.Areas[_hit - 1].DamageGroup) _groupTargets.Clear();
                        _elapsed = 0; _attackWindow.ClearTargets();
                        if (_pattern.Kind == BossPatternKind.TripleSlam) Facing = _targetDirection;
                        UpdateAirborne();
                        _attackWindow.SetActive(_pattern.Hits[_hit].Windup == 0);
                        if (_pattern == null) break;
                        continue;
                    }
                    ClearPattern();
                    Boss.States.Change(new IdleState());
                    _waitRemaining = Pick(Boss.Phase.IdleMin, checked(Boss.Phase.IdleMax + 1));
                }
                else if (_waitRemaining > 0)
                {
                    int step = Math.Min(milliseconds, _waitRemaining);
                    _waitRemaining -= step; milliseconds -= step;
                }
                else break;
            }
        }

        // 돌진 중 벽 충돌 관측. 돌진을 끝내 후딜로 넘기고 체세를 깎습니다(체세 0이면 노출이 패턴을 정리). 돌진 중이 아니면 무시합니다.
        public bool NotifyWallHit()
        {
            if (!IsCharging) return false;
            AttackSpec spec = _pattern.Hits[_hit];
            _elapsed = spec.Windup + spec.Active;
            _attackWindow.SetActive(false);
            if (_pattern.Kind == BossPatternKind.CrawlCharge) Boss.States.Call(StateKey.Stun);
            Boss.ReceiveHit(0, _pattern.WallPoiseDamage);
            return true;
        }

        // 비활성화·풀 반환 시 진행 중 패턴·대기를 정리합니다. HP·체세는 유지합니다.
        public void Release()
        {
            bool attacking = _pattern != null;
            ClearPattern(); _waitRemaining = 0;
            if (attacking && !IsBlocked) Boss.States.Change(new IdleState());
        }

        private bool IsBlocked => Boss.IsDead || Boss.IsExposed || Boss.IsTransitioning || Boss.CurrentState == StateKey.Execute;

        private HitOutcome HitPlayer(int targetId, PlayerActor target, Func<int, int, int> random, bool dodged, bool critical, int direction = 0)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (_pattern != null && _pattern.Areas.Count > 0 && _pattern.Areas[_hit].DamageGroup > 0 && _groupTargets.Contains(targetId)) return HitOutcome.None;
            if (!IsBlocked && _pattern != null && !_pattern.IsChain && (CurrentReaction == null || CurrentReaction.Parryable) && target.TryParryContact(_attackWindow, targetId, direction))
            {
                Boss.ReceiveHit(0, target.ParryPoiseDamage);
                return HitOutcome.Parried;
            }
            HitOutcome outcome = HitRoll.Resolve(_attackWindow, targetId, target.CombatStats.Dodge, Boss.Definition.Crit,
                IsBlocked || target.IsDead || target.IsInvulnerable, true, random, dodged, critical);
            if (outcome < HitOutcome.Hit) return outcome;
            _groupTargets.Add(targetId);
            bool isCritical = outcome == HitOutcome.Critical;
            int damage = CombatMath.Damage(Boss.Definition, target.CombatStats, _pattern.Hits[_hit].ExtraDamage, !_pattern.IsChain && isCritical);
            if (_pattern.IsChain)
            {
                if (!target.TryChainBind(_pattern.BindTime, _pattern.BindPresses, damage)) return HitOutcome.None;
                _chained = target.IsDead ? null : target;
                return outcome;
            }
            bool received = CurrentReaction == null ? target.ReceiveHit(damage, isCritical) : target.ReceiveBossHit(damage, isCritical, CurrentReaction);
            return received ? outcome : HitOutcome.None;
        }

        private void OnBossChanged()
        {
            if (!IsBlocked) return;
            ClearPattern(); _waitRemaining = 0;
        }

        private int Pick(int min, int maxExclusive)
        {
            int value = _random(min, maxExclusive);
            if (value < min || value >= maxExclusive) throw new InvalidOperationException("난수 공급 범위를 벗어났습니다.");
            return value;
        }

        private void ClearPattern()
        {
            _attackWindow.SetActive(false);
            _pattern = null; _attackWindow.ClearTargets(); _groupTargets.Clear(); UpdateAirborne();
            if (_chained != null) { _chained.ReleaseChain(); _chained = null; }
        }
    }
}
