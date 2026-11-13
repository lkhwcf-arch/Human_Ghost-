using System;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Storage;
using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    // 제공된 일반몹 정의와 패턴 설정으로 개체별 AI 런타임을 조립합니다. 관측·이동·판정은 외부 어댑터가 공급합니다.
    public sealed class MonsterRuntime : SpatialRuntime
    {
        [SerializeField, Min(1)] private int _monsterId = 3001;
        [SerializeField] private MonsterPatternSettings _patterns = null;
        private System.Random _random;
        private System.Random _hitRandom;
        private MillisecondClock _clock;

        public int MonsterId => _monsterId;
        public MonsterPatternSettings Patterns => _patterns;
        public Monster Definition { get; private set; }
        public MonsterActor Actor { get; private set; }
        // 이 몬스터가 공격자일 때의 회피·치명 난수. 패턴/대기 난수열과 분리합니다.
        public Func<int, int, int> HitRandom { get; private set; }
        public bool IsInitialized => Definition != null;
#if UNITY_EDITOR
        // 정지 테스트 개체만 사용합니다. 상태 변경·애니메이션은 그대로 작동합니다.
        public bool FreezeForEditorTest { get; set; }
#endif
        public event Action<Monster> Initialized;

        public void Initialize(IReadOnlyDataCatalog catalog, bool keepHealth = false)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (IsInitialized) throw new InvalidOperationException("이미 초기화된 일반 몬스터입니다.");
            if (_patterns == null) throw new InvalidOperationException("일반몹 패턴 설정을 연결하세요.");
            Monster definition = catalog.GetTable<Monster>().Get(_monsterId);
            // 같은 프레임에 생성돼도 개체마다 다른 난수열을 쓰도록 인스턴스 ID를 섞습니다.
            _random = new System.Random(Environment.TickCount ^ GetInstanceID());
            _hitRandom = new System.Random(Environment.TickCount ^ ~GetInstanceID());
            Actor = new MonsterActor(definition, _patterns.CreatePatterns(), _patterns.HitStun, _patterns.CritHitStun, _random.Next, keepHealth);
            HitRandom = _hitRandom.Next;
            Definition = definition;
            Initialized?.Invoke(definition);
        }

        // 비활성화 시 남은 구속·판정·돌진을 정리합니다. 재활성 후 새 관측부터 다시 판단합니다.
        private void OnEnable() => NotifyAvailability(true);
        private void OnDisable()
        {
            SubscriptionScope.Cleanup(() => NotifyAvailability(false), () => Actor?.Release());
        }

        private void Update()
        {
#if UNITY_EDITOR
            if (FreezeForEditorTest) return;
#endif
            if (Actor == null || Actor.IsDead) return;
            Actor.Tick(_clock.Advance(Time.deltaTime));
        }
    }
}
