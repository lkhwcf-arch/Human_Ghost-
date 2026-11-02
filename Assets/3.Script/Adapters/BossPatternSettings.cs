using System;
using HumanGhost.Data.Definitions;
using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    // 전투 R2 보스 기본 패턴(내려찍기·2연타)의 수신 설정. 기획 제안 시작값은 선딜·추가 피해만 있고
    // 판정·후딜·2연타 간격은 미공급입니다. 0으로 남은 판정 시간은 초기화 때 누락으로 거부합니다. 값 결정·튜닝은 기획 소유입니다.
    [CreateAssetMenu(menuName = "인귀/보스 패턴 설정")]
    public sealed class BossPatternSettings : ScriptableObject
    {
        [Serializable]
        private sealed class HitSetup
        {
            [SerializeField] private int _windup;
            [SerializeField] private int _active;
            [SerializeField] private int _recovery;
            [SerializeField] private int _extraDamage;

            public AttackSpec Create(StateKey key, int index)
            {
                if (_windup <= 0 || _active <= 0 || _recovery < 0)
                    throw new InvalidOperationException($"보스 패턴 {key} {index + 1}타의 선딜·판정·후딜 시간을 설정하세요.");
                return new AttackSpec(_windup, _active, _recovery, 0, _extraDamage, 0, 0);
            }
        }

        [Serializable]
        private sealed class PatternSetup
        {
            [SerializeField] private StateKey _key = StateKey.Attack1;
            [SerializeField] private HitSetup[] _hits = Array.Empty<HitSetup>();

            public BossPattern Create()
            {
                if (_hits == null || _hits.Length == 0) throw new InvalidOperationException($"보스 패턴 {_key}의 타격을 1개 이상 설정하세요.");
                var hits = new AttackSpec[_hits.Length];
                for (int i = 0; i < hits.Length; i++)
                    hits[i] = (_hits[i] ?? throw new InvalidOperationException("보스 타격 설정이 비어 있습니다.")).Create(_key, i);
                return new BossPattern(_key, hits);
            }
        }

        [SerializeField] private PatternSetup[] _patterns = Array.Empty<PatternSetup>();

        public BossPattern[] CreatePatterns()
        {
            if (_patterns == null || _patterns.Length == 0) throw new InvalidOperationException("보스 패턴을 1개 이상 설정하세요.");
            var result = new BossPattern[_patterns.Length];
            for (int i = 0; i < result.Length; i++)
                result[i] = (_patterns[i] ?? throw new InvalidOperationException("보스 패턴 설정이 비어 있습니다.")).Create();
            return result;
        }
    }
}
