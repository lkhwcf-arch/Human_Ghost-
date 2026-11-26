using System;
namespace HumanGhost.Gameplay
{
    // 거리·발판 체류 관측만 받습니다. 타격/이동/그림 없이 독립적으로 선택 규칙을 검사할 수 있습니다.
    // 선택 순서는 이 코드가, 기준값(거리·발판 대기·횟수·체력 비율)은 현재 페이즈의 BossState가 정합니다(11/19 데이터 이전).
    public sealed class BossPhaseTwoSelector
    {
        private readonly BossCombat _combat;
        private BossPatternKind _last, _close = BossPatternKind.Sweep;
        // 첫 패턴은 항상 기어 돌진이므로 돌진 뒤 경과 횟수는 0에서 시작합니다.
        private int _sinceCharge, _regulars;
        private bool _first = true, _desperation, _nextLeap;
        public BossPhaseTwoSelector(BossCombat combat) { _combat = combat ?? throw new ArgumentNullException(nameof(combat)); }
        // 체력 기준은 BossCombat.IsDesperate 한 곳이 소유합니다. 선택과 시작 허용이 어긋나지 않게 합니다(조건 규칙 감사 3.14).
        public bool DesperationPending => !_first && !_desperation && _combat.IsDesperate;
        public int Choose(int distance, int platformMilliseconds)
        {
            if (distance < 0 || platformMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(distance));
            bool low = _combat.IsDesperate;
            var rule = _combat.Boss.Phase;
            BossPatternKind kind;
            if (_first) kind = BossPatternKind.CrawlCharge;
            else if (low && !_desperation) kind = BossPatternKind.DesperationLeap;
            else if (_regulars >= rule.SlamAfterCount) kind = low && _nextLeap ? BossPatternKind.DesperationLeap : BossPatternKind.GroundSlam;
            else if (platformMilliseconds > rule.PlatformWaitTime && _last != BossPatternKind.Pillar) kind = BossPatternKind.Pillar;
            else if (distance <= rule.CloseRange) kind = _close == BossPatternKind.Sweep ? BossPatternKind.Pillar : BossPatternKind.Sweep;
            else if (distance <= rule.MidRange) kind = BossPatternKind.TripleSlam;
            else if (_sinceCharge >= rule.ChargeGap) kind = BossPatternKind.CrawlCharge;
            else return -1;
            if (kind == _last)
            {
                if (distance > rule.CloseRange) return -1;
                kind = _last == BossPatternKind.Pillar ? BossPatternKind.Sweep : BossPatternKind.Pillar;
            }
            for (int i = 0; i < _combat.PatternCount; i++)
            {
                BossPattern pattern = _combat.GetPattern(i);
                if (pattern.Kind == kind && rule.Phase >= pattern.MinPhase && rule.Phase <= pattern.MaxPhase) return i;
            }
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
