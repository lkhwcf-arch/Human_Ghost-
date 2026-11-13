using System;
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
        // 선택: 연결하지 않으면 체세·노출·해체만 동작하고 패턴 실행 요청은 누락 오류로 거부합니다.
        [SerializeField] private BossPatternSettings _patterns = null;
        private MillisecondClock _clock;
        public int TransitionMilliseconds { get; private set; }
        public float LandingX { get; set; }
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
            var patterns = _patterns != null ? _patterns.CreatePatterns() : null;
            bool transition = patterns != null && Array.Exists(patterns, p => p.Kind != BossPatternKind.Legacy);
            var actor = new BossPoise(definition, phase, transition);
            if (_patterns != null)
            {
                _waitRandom = new System.Random(Environment.TickCount ^ GetInstanceID());
                _hitRandom = new System.Random(Environment.TickCount ^ ~GetInstanceID());
                Combat = new BossCombat(actor, patterns, _waitRandom.Next);
                HitRandom = _hitRandom.Next;
            }
            Actor = actor;
            Initialized?.Invoke(Actor);
        }

        // 외부 패턴 선택 요청. 설정 미연결은 조용히 무시하지 않고 알립니다.
        public bool TryStartPattern(int index)
        {
            if (Actor == null) throw new InvalidOperationException("보스 초기화가 필요합니다.");
            if (Combat == null) throw new InvalidOperationException("보스 패턴 설정을 연결하세요.");
            return Combat.TryStart(index);
        }

        private void OnDisable() => Combat?.Release();

        private void Update()
        {
            if (Actor == null) return;
            int milliseconds = _clock.Advance(Time.deltaTime);
            TransitionMilliseconds = Actor.IsTransitioning ? Math.Min(1000, TransitionMilliseconds + milliseconds) : 0;
            Actor.Tick(milliseconds);
            Combat?.Tick(milliseconds);
        }
    }
}
