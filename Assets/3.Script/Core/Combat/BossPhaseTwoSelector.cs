using System;
namespace HumanGhost.Gameplay
{
    // 거리·발판 체류 관측만 받습니다. 타격/이동/그림 없이 독립적으로 선택 규칙을 검사할 수 있습니다.
    public sealed class BossPhaseTwoSelector
    {
        private readonly BossCombat _combat;
        private BossPatternKind _last, _close = BossPatternKind.Sweep;
        private int _sinceCharge = 2, _regulars;
        private bool _first = true, _desperation, _nextLeap;
        public BossPhaseTwoSelector(BossCombat combat) { _combat = combat ?? throw new ArgumentNullException(nameof(combat)); }
        public bool DesperationPending => !_first && !_desperation && (long)_combat.Boss.Health * 4 <= _combat.Boss.Definition.HP;
        public int Choose(int distance, int platformMilliseconds)
        {
            if (distance < 0 || platformMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(distance));
            bool low = (long)_combat.Boss.Health * 4 <= _combat.Boss.Definition.HP;
            BossPatternKind kind;
            if (_first) kind = BossPatternKind.CrawlCharge;
            else if (low && !_desperation) kind = BossPatternKind.DesperationLeap;
            else if (_regulars >= 3) kind = low && _nextLeap ? BossPatternKind.DesperationLeap : BossPatternKind.GroundSlam;
            else if (platformMilliseconds > 3000 && _last != BossPatternKind.Pillar) kind = BossPatternKind.Pillar;
            else if (distance <= 300) kind = _close == BossPatternKind.Sweep ? BossPatternKind.Pillar : BossPatternKind.Sweep;
            else if (distance <= 600) kind = BossPatternKind.TripleSlam;
            else if (_sinceCharge >= 2) kind = BossPatternKind.CrawlCharge;
            else return -1;
            if (kind == _last)
            {
                if (distance > 300) return -1;
                kind = _last == BossPatternKind.Pillar ? BossPatternKind.Sweep : BossPatternKind.Pillar;
            }
            for (int i = 0; i < _combat.PatternCount; i++) if (_combat.GetPattern(i).Kind == kind) return i;
            throw new InvalidOperationException("2페이즈 필수 패턴이 없습니다: " + kind);
        }
        public void Started(BossPattern pattern)
        {
            if (pattern.Kind == BossPatternKind.Legacy) return;
            _first = false; _last = pattern.Kind;
            if (_last == BossPatternKind.CrawlCharge) _sinceCharge = 0; else _sinceCharge++;
            if (_last == BossPatternKind.Pillar || _last == BossPatternKind.Sweep) _close = _last;
            if (_last == BossPatternKind.GroundSlam || _last == BossPatternKind.DesperationLeap)
            { _regulars = 0; _nextLeap = _last == BossPatternKind.GroundSlam; }
            else _regulars++;
            if (_last == BossPatternKind.DesperationLeap) _desperation = true;
        }
    }
}
