using System;
namespace HumanGhost.Gameplay
{
    public static class BossHitGeometry
    {
        // 월드 좌표를 타일×100으로 변환한 뒤 사용. 발판 경계(높이 2/4)의 안전 구간을 몸체 그림과 분리합니다.
        public static bool Contains(BossHitArea area, int x, int feet, int head, int facing)
        {
            if (area == null) throw new ArgumentNullException(nameof(area));
            return Contains(area, area.Range, x, feet, head, facing);
        }
        // range: 지금 실제로 닿는 거리(충격파는 퍼지는 중의 반지름). 높이·방향 규칙은 area를 그대로 씁니다.
        public static bool Contains(BossHitArea area, int range, int x, int feet, int head, int facing)
        {
            if (area == null) throw new ArgumentNullException(nameof(area));
            if (facing != -1 && facing != 1) throw new ArgumentOutOfRangeException(nameof(facing));
            if (range < 0) throw new ArgumentOutOfRangeException(nameof(range));
            return head > 0 && feet < area.Height && Math.Abs((long)x) <= range && (area.Radial || (long)x * facing >= 0);
        }

        // 내려찍기 뒤 선딜 없이 이어지는 원형 충격파의 현재 반지름(tile_x100)입니다. 판정과 바닥 흙먼지 원이 이 값 하나를 씁니다.
        // 내려찍기 판정이 열린 순간 0에서 시작해 두 판정 창(내려찍기+충격파) 동안 충격파 범위까지 일정하게 퍼집니다.
        // 충격파와 관계없는 칸은 -1입니다.
        public static int ShockwaveReach(BossPattern pattern, int hit, int hitElapsed)
        {
            if (pattern == null) throw new ArgumentNullException(nameof(pattern));
            if (pattern.Areas.Count == 0 || hit < 0 || hit >= pattern.Hits.Count) return -1;
            int wave, since;
            if (IsShockwave(pattern, hit)) { wave = hit; since = pattern.Hits[hit - 1].Active + Math.Min(Math.Max(0, hitElapsed), pattern.Hits[hit].Active); }
            else if (hit + 1 < pattern.Hits.Count && IsShockwave(pattern, hit + 1) && hitElapsed >= pattern.Hits[hit].Windup)
            { wave = hit + 1; since = Math.Min(hitElapsed - pattern.Hits[hit].Windup, pattern.Hits[hit].Active); }
            else return -1;
            int total = pattern.Hits[wave - 1].Active + pattern.Hits[wave].Active;
            return (int)((long)pattern.Areas[wave].Range * since / total);
        }
        public static bool IsShockwave(BossPattern pattern, int hit)
            => hit > 0 && hit < pattern.Hits.Count && pattern.Areas.Count > hit && pattern.Areas[hit].Radial && pattern.Hits[hit].Windup == 0;
    }
}
