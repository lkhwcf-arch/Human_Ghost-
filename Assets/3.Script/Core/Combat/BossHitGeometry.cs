using System;
namespace HumanGhost.Gameplay
{
    public static class BossHitGeometry
    {
        // 월드 좌표를 타일×100으로 변환한 뒤 사용. 발판 경계(높이 2/4)의 안전 구간을 몸체 그림과 분리합니다.
        public static bool Contains(BossHitArea area, int x, int feet, int head, int facing)
        {
            if (area == null) throw new ArgumentNullException(nameof(area));
            if (facing != -1 && facing != 1) throw new ArgumentOutOfRangeException(nameof(facing));
            return head > 0 && feet < area.Height && Math.Abs((long)x) <= area.Range && (area.Radial || (long)x * facing >= 0);
        }
    }
}
