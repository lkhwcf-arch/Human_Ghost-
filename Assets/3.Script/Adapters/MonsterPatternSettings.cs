using System;
using HumanGhost.Data.Definitions;
using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    // 전투 R2 C08: 일반몹 패턴 추가 데미지·타이밍은 개발 값(Attack 탭 삭제). 몬스터 종류마다 에셋 하나, 다음 런타임 초기화에 반영.
    [CreateAssetMenu(menuName = "인귀/일반몹 패턴 설정")]
    public sealed class MonsterPatternSettings : ScriptableObject
    {
        [Serializable]
        private sealed class PatternSetup
        {
            [SerializeField] private StateKey _key = StateKey.Attack1;
            [SerializeField] private int _range;
            [SerializeField] private int _windup;
            [SerializeField] private int _active;
            [SerializeField] private int _recovery;
            [SerializeField] private int _extraDamage;
            [SerializeField] private int _bindTime;
            [SerializeField] private int _bindPresses;
            [SerializeField] private int _chargeSpeed;
            [SerializeField] private int _wallStun;

            public MonsterPattern Create() => new MonsterPattern(_key, _range, new AttackSpec(_windup, _active, _recovery, 0, _extraDamage, 0, 0),
                _bindTime, _bindPresses, _chargeSpeed, _wallStun);
        }

        [SerializeField] private PatternSetup[] _patterns = Array.Empty<PatternSetup>();
        [SerializeField] private int _hitStun;
        [SerializeField] private int _critHitStun;

        public int HitStun => _hitStun;
        public int CritHitStun => _critHitStun;

        public MonsterPattern[] CreatePatterns()
        {
            if (_patterns == null || _patterns.Length == 0) throw new InvalidOperationException("일반몹 패턴을 1개 이상 설정하세요.");
            var result = new MonsterPattern[_patterns.Length];
            for (int i = 0; i < result.Length; i++)
                result[i] = (_patterns[i] ?? throw new InvalidOperationException("패턴 설정이 비어 있습니다.")).Create();
            return result;
        }
    }
}
