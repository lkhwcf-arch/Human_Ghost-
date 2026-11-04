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
            [SerializeField] private int _range = 0;
            [SerializeField] private int _windup = 0;
            [SerializeField] private int _active = 0;
            [SerializeField] private int _recovery = 0;
            [SerializeField] private int _extraDamage = 0;
            [SerializeField] private int _bindTime = 0;
            [SerializeField] private int _bindPresses = 0;
            [SerializeField] private int _chargeSpeed = 0;
            [SerializeField] private int _wallStun = 0;
            [SerializeField] private int _minRange = 0;
            [SerializeField] private bool _projectile = false;

            public MonsterPattern Create() => new MonsterPattern(_key, _range, new AttackSpec(_windup, _active, _recovery, 0, _extraDamage, 0, 0),
                _bindTime, _bindPresses, _chargeSpeed, _wallStun, _minRange, _projectile);
        }

        [SerializeField] private PatternSetup[] _patterns = Array.Empty<PatternSetup>();
        [SerializeField] private int _hitStun = 0;
        [SerializeField] private int _critHitStun = 0;
        [Header("죽을 때 남기는 불 (C08 불붙은 허수귀, 0이면 없음)")]
        [SerializeField, Min(0)] private int _deathFireTime = 0;
        [SerializeField, Min(0)] private int _deathFireDamage = 0;
        [SerializeField, Min(0)] private int _deathFireHalfWidth = 0;

        public int HitStun => _hitStun;
        public int CritHitStun => _critHitStun;
        // 불 범위 절반 폭(tile_x100). 시간이 0이면 불을 남기지 않습니다.
        public bool LeavesDeathFire => _deathFireTime > 0;
        public int DeathFireHalfWidth => _deathFireHalfWidth;
        public GroundFire CreateDeathFire() => _deathFireTime > 0 ? new GroundFire(_deathFireTime, _deathFireDamage, 1000) : null;

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
