using System;
using System.Collections.Generic;

namespace HumanGhost.Gameplay
{
    // K15 상호작용 대상 하나의 관측값입니다.
    public readonly struct InteractionCandidate
    {
        public readonly long X, Y;
        public readonly bool CanInteract;
        public InteractionCandidate(long x, long y, bool canInteract) { X = x; Y = y; CanInteract = canInteract; }
    }

    // 범위 안 CanInteract 대상 중 가로 거리가 가장 가까운 하나를 고릅니다(K15). 없으면 -1입니다.
    public static class InteractionSelector
    {
        public static int Pick(long x, long y, IReadOnlyList<InteractionCandidate> candidates, long rangeX, long rangeY)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (rangeX < 0 || rangeY < 0) throw new ArgumentOutOfRangeException(nameof(rangeX));
            int best = -1;
            ulong bestDistance = ulong.MaxValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                InteractionCandidate candidate = candidates[i];
                ulong distance = Distance(candidate.X, x);
                if (!candidate.CanInteract || distance > (ulong)rangeX || Distance(candidate.Y, y) > (ulong)rangeY || distance >= bestDistance) continue;
                best = i; bestDistance = distance;
            }
            return best;
        }

        // long 양끝 좌표의 차이도 부호 있는 뺄셈·Math.Abs 오버플로 없이 비교합니다.
        private static ulong Distance(long left, long right) => left >= right
            ? unchecked((ulong)left - (ulong)right) : unchecked((ulong)right - (ulong)left);
    }
}
