using System;
using System.Collections.Generic;
using HumanGhost.Data.Definitions;

namespace HumanGhost.Gameplay
{
    // 보스 기본 패턴 하나의 읽기 전용 실행 인수. 타격마다 AttackSpec 하나(선딜·판정·후딜, 추가 피해)이며 순서대로 실행합니다.
    // 2연타는 판정 창 두 개입니다. 둘째 타의 Windup이 첫 타 후 간격, 마지막 타의 Recovery가 패턴 후딜입니다.
    public sealed class BossPattern
    {
        public StateKey Key { get; }
        public IReadOnlyList<AttackSpec> Hits { get; }

        public BossPattern(StateKey key, IReadOnlyList<AttackSpec> hits)
        {
            if (key != StateKey.Attack1 && key != StateKey.Attack2) throw new ArgumentOutOfRangeException(nameof(key));
            if (hits == null || hits.Count == 0) throw new ArgumentException("타격 설정이 1개 이상 필요합니다.", nameof(hits));
            var copy = new AttackSpec[hits.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = hits[i] ?? throw new ArgumentException("빈 타격 설정입니다.", nameof(hits));
            Key = key;
            Hits = copy;
        }
    }
}
