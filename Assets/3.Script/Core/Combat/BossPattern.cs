using System;
using System.Collections.Generic;
using HumanGhost.Data.Definitions;

namespace HumanGhost.Gameplay
{
    // 보스 기본 패턴 하나의 읽기 전용 실행 인수. 타격마다 AttackSpec 하나(선딜·판정·후딜, 추가 피해)이며 순서대로 실행합니다.
    // 2연타는 판정 창 두 개입니다. 둘째 타의 Windup이 첫 타 후 간격, 마지막 타의 Recovery가 패턴 후딜입니다.
    // Skill1 문 돌진(C09): 판정 창 동안 ChargeSpeed로 이동하며 Active는 돌진 상한입니다. 벽 충돌 시 창을 닫고 체세 WallPoiseDamage를 깎습니다.
    public enum BossPatternKind { Legacy, Pillar, TripleSlam, Sweep, CrawlCharge, GroundSlam, DesperationLeap }

    // 타일×100. 같은 DamageGroup의 직격·울림은 한 대상에게 중복 피해를 주지 않습니다.
    public sealed class BossHitArea
    {
        public int Range { get; }
        public int Height { get; }
        public bool Radial { get; }
        public int DamageGroup { get; }
        public BossHitArea(int range, int height, bool radial, int damageGroup)
        {
            if (range <= 0 || height <= 0 || damageGroup < 0) throw new ArgumentOutOfRangeException(nameof(range));
            Range = range; Height = height; Radial = radial; DamageGroup = damageGroup;
        }
    }

    public sealed class BossPattern
    {
        public BossPatternKind Kind { get; }
        public int MaxPhase { get; }
        public IReadOnlyList<BossHitArea> Areas { get; }
        public IReadOnlyList<BossHitReaction> Reactions { get; }
        public StateKey Key { get; }
        public IReadOnlyList<AttackSpec> Hits { get; }
        // 이 패턴을 시작할 수 있는 최소 BossState.Phase(0부터). C09 문 돌진은 HP 50% 이하 페이즈(데이터 Phase 1)부터입니다.
        public int MinPhase { get; }
        // 돌진 속도(tile_x100/초). 0이면 이동 없는 패턴입니다.
        public int ChargeSpeed { get; }
        public int WallPoiseDamage { get; }
        public bool IsCharge => ChargeSpeed > 0;
        // Attack3 사슬 당기기(C09): 멀리서 잡아 끌어옵니다. Range는 사슬이 닿는 거리(tile_x100), PullSpeed는 끄는 속도입니다.
        public int Range { get; }
        public int BindTime { get; }
        public int BindPresses { get; }
        public int PullSpeed { get; }
        public bool IsChain => BindPresses > 0;

        public BossPattern(StateKey key, IReadOnlyList<AttackSpec> hits, int minPhase = 0, int chargeSpeed = 0, int wallPoiseDamage = 0,
            int range = 0, int bindTime = 0, int bindPresses = 0, int pullSpeed = 0,
            BossPatternKind kind = BossPatternKind.Legacy, int maxPhase = int.MaxValue, IReadOnlyList<BossHitArea> areas = null, IReadOnlyList<BossHitReaction> reactions = null)
        {
            if (key != StateKey.Attack1 && key != StateKey.Attack2 && key != StateKey.Attack3 && key != StateKey.Skill1) throw new ArgumentOutOfRangeException(nameof(key));
            if (range < 0 || bindTime < 0 || bindPresses < 0 || pullSpeed < 0 || (key == StateKey.Attack3) != (bindPresses > 0) ||
                (bindPresses > 0 && (range == 0 || pullSpeed == 0 || hits == null || hits.Count != 1)))
                throw new ArgumentOutOfRangeException(nameof(key), "보스 사슬 패턴 설정을 확인하세요.");
            if (hits == null || hits.Count == 0) throw new ArgumentException("타격 설정이 1개 이상 필요합니다.", nameof(hits));
            // 돌진은 Skill1 단일 타격만. 벽 체세 피해는 돌진에만 있습니다.
            if (minPhase < 0 || chargeSpeed < 0 || wallPoiseDamage < 0 || (key == StateKey.Skill1 || kind == BossPatternKind.CrawlCharge) != (chargeSpeed > 0) ||
                (chargeSpeed > 0 && hits.Count != 1) || (chargeSpeed == 0 && wallPoiseDamage > 0))
                throw new ArgumentOutOfRangeException(nameof(key), "보스 패턴 페이즈·돌진 설정을 확인하세요.");
            var copy = new AttackSpec[hits.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = hits[i] ?? throw new ArgumentException("빈 타격 설정입니다.", nameof(hits));
            if (!Enum.IsDefined(typeof(BossPatternKind), kind) || maxPhase < minPhase ||
                (kind != BossPatternKind.Legacy && (minPhase < 1 || areas == null || areas.Count != hits.Count)) ||
                (areas != null && areas.Count != hits.Count)) throw new ArgumentException("보스 패턴 종류·판정 영역을 확인하세요.");
            var areaCopy = new BossHitArea[areas?.Count ?? 0];
            for (int i = 0; i < areaCopy.Length; i++) areaCopy[i] = areas[i] ?? throw new ArgumentException("빈 보스 판정 영역입니다.");
            if (reactions != null && reactions.Count != hits.Count) throw new ArgumentException("보스 타격별 반응 수를 확인하세요.");
            var reactionCopy = new BossHitReaction[reactions?.Count ?? 0];
            for(int i=0;i<reactionCopy.Length;i++) reactionCopy[i]=reactions[i]??throw new ArgumentException("빈 보스 피격 반응입니다.");
            Reactions=Array.AsReadOnly(reactionCopy);
            Kind = kind; MaxPhase = maxPhase; Areas = Array.AsReadOnly(areaCopy);
            Key = key; MinPhase = minPhase; ChargeSpeed = chargeSpeed; WallPoiseDamage = wallPoiseDamage;
            Range = range; BindTime = bindTime; BindPresses = bindPresses; PullSpeed = pullSpeed;
            // 배열을 그대로 노출하면 형변환으로 바꿀 수 있어 읽기 전용으로 감쌉니다.
            Hits = Array.AsReadOnly(copy);
        }
    }
}
