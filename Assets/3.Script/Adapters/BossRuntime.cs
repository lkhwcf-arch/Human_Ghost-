using System;
using System.Collections.Generic;
using System.Linq;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Storage;
using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    public sealed class BossRuntime : SpatialRuntime
    {
        public event Action PositionChanged { add => SpatialChanged += value; remove => SpatialChanged -= value; }
        [SerializeField] private int _bossId = 1006;
        [SerializeField] private int _phase = 0;
        // 패턴 시작 효과음 키(BossSkill.SoundKey)입니다. 패턴 객체로 찾습니다.
        private readonly Dictionary<BossPattern, string> _sounds = new Dictionary<BossPattern, string>();
        private MillisecondClock _clock;
        public int TransitionMilliseconds { get; private set; }
        private System.Random _waitRandom;
        private System.Random _hitRandom;
        public BossPoise Actor { get; private set; }
        public BossCombat Combat { get; private set; }
        // 이 보스가 공격자일 때의 회피·치명 난수. 패턴 간 대기 난수와 분리합니다.
        public Func<int, int, int> HitRandom { get; private set; }
        public event Action<BossPoise> Initialized;

        public void Initialize(IReadOnlyDataCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (Actor != null) throw new InvalidOperationException("이미 초기화된 보스입니다.");
            Boss definition = catalog.GetTable<Boss>().Get(_bossId);
            BossState phase = null;
            foreach (BossState entry in definition.States) if (entry.Phase == _phase) phase = entry;
            if (phase == null) throw new InvalidOperationException("선택한 보스 단계가 없습니다.");
            // 패턴은 BossSkill(이 보스 행, ID 순)과 BossSkillHit(BossSkillID 행, ID 순)이 정합니다(11/19 BossPatterns.asset에서 이전). 행이 없으면 공격하지 않습니다.
            BossSkillHit[] hitRows = catalog.GetTable<BossSkillHit>().All.OrderBy(row => row.ID).ToArray();
            BossSkill[] skills = catalog.GetTable<BossSkill>().All.Where(row => row.BossID == definition.ID).OrderBy(row => row.ID).ToArray();
            BossPattern[] patterns = skills.Length == 0 ? null : skills.Select(skill => CreatePattern(skill, hitRows.Where(hit => hit.BossSkillID == skill.ID).ToArray())).ToArray();
            foreach (BossState state in definition.States) ValidatePhaseTiming(state, patterns);
            _sounds.Clear();
            for (int i = 0; patterns != null && i < patterns.Length; i++) _sounds.Add(patterns[i], skills[i].SoundKey);
            bool transition = patterns != null && Array.Exists(patterns, p => p.Kind != BossPatternKind.Legacy);
            var actor = new BossPoise(definition, phase, transition);
            if (patterns != null)
            {
                _waitRandom = new System.Random(Environment.TickCount ^ GetInstanceID());
                _hitRandom = new System.Random(Environment.TickCount ^ ~GetInstanceID());
                Combat = new BossCombat(actor, patterns, _waitRandom.Next);
                HitRandom = _hitRandom.Next;
            }
            Actor = actor;
            Initialized?.Invoke(Actor);
        }

        // 패턴 시작 효과음 키입니다. 데이터가 비웠으면 빈 문자열입니다.
        public string SoundKeyOf(BossPattern pattern) => pattern != null && _sounds.TryGetValue(pattern, out string key) ? key : "";

        // BossSkill 한 행과 그 타격 행들로 패턴을 만듭니다. 종류·페이즈·돌진·사슬 조합 검증은 BossPattern이 합니다.
        private static BossPattern CreatePattern(BossSkill skill, BossSkillHit[] hits)
        {
            if (hits.Length == 0) throw new InvalidOperationException($"보스 패턴 {skill.ID}의 타격(BossSkillHit)을 1개 이상 설정하세요.");
            var kind = (BossPatternKind)Enum.Parse(typeof(BossPatternKind), skill.Kind);
            AttackSpec[] specs = hits.Select(hit => new AttackSpec(hit.Windup, hit.Active, hit.Recovery, 0, hit.ExtraDamage, 0, 0, immediate: hit.Windup == 0)).ToArray();
            // 사슬은 범위가 비어 있으면 ChainRange를 씁니다. 그 외 타격은 표의 판정과 예고가 동일한 범위를 사용합니다.
            bool usesChainRange = skill.BindPresses > 0 && hits.All(hit => hit.Range == 0 && hit.Height == 0);
            if (!usesChainRange && hits.Any(hit => hit.Range <= 0 || hit.Height <= 0))
                throw new InvalidOperationException($"보스 패턴 {skill.ID}의 각 타격에 양수 Range와 Height가 필요합니다.");
            BossHitArea[] areas = usesChainRange ? null :
                hits.Select(hit => new BossHitArea(hit.Range, hit.Height, hit.Radial, hit.DamageGroup)).ToArray();
            BossHitReaction[] reactions = skill.HitReactions
                ? hits.Select(hit => new BossHitReaction(hit.KnockbackDistance, hit.KnockbackTime, hit.DownTime, hit.SlowRate, hit.SlowTime, hit.Parryable)).ToArray() : null;
            return new BossPattern((StateKey)skill.StateID, specs, skill.MinPhase, skill.ChargeSpeed, skill.WallPoiseDamage, skill.ChainRange,
                skill.BindTime, skill.BindPresses, skill.PullSpeed, kind, skill.MaxPhase, areas, reactions);
        }

        private static void ValidatePhaseTiming(BossState phase, BossPattern[] patterns)
        {
            if (patterns == null) return;
            foreach (BossPattern pattern in patterns)
            {
                if (pattern.Kind == BossPatternKind.DesperationLeap && phase.Phase >= pattern.MinPhase && phase.Phase <= pattern.MaxPhase &&
                    phase.LeapAirTime > pattern.Hits[0].Windup)
                    throw new InvalidOperationException($"보스 발악 도약의 체공 시간({phase.LeapAirTime}ms)이 첫 타 선딜({pattern.Hits[0].Windup}ms)보다 깁니다.");
            }
        }

        // 외부 패턴 선택 요청. 패턴 데이터가 없으면 조용히 무시하지 않고 알립니다.
        public bool TryStartPattern(int index)
        {
            if (Actor == null) throw new InvalidOperationException("보스 초기화가 필요합니다.");
            if (Combat == null) throw new InvalidOperationException("이 보스의 BossSkill 패턴 데이터가 없습니다.");
            return Combat.TryStart(index);
        }

        private void OnDisable() => Combat?.Release();

        private void Update()
        {
            if (Actor == null) return;
            int milliseconds = _clock.AdvanceFrame(Time.deltaTime);
            TransitionMilliseconds = Actor.IsTransitioning ? Math.Min(1000, TransitionMilliseconds + milliseconds) : 0;
            Actor.Tick(milliseconds);
            Combat?.Tick(milliseconds);
        }
    }
}
