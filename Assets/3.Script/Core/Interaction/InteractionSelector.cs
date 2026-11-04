using System;
using System.Collections.Generic;

namespace HumanGhost.Gameplay
{
    // K15 상호작용 대상 하나의 관측값입니다.
    public readonly struct InteractionCandidate
    {
        public readonly float X, Y;
        public readonly bool CanInteract;
        public InteractionCandidate(float x, float y, bool canInteract) { X = x; Y = y; CanInteract = canInteract; }
    }

    // 범위 안 CanInteract 대상 중 가로 거리가 가장 가까운 하나를 고릅니다(K15). 없으면 -1입니다.
    public static class InteractionSelector
    {
        public static int Pick(float x, float y, IReadOnlyList<InteractionCandidate> candidates, float rangeX, float rangeY)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                InteractionCandidate candidate = candidates[i];
                float distance = Math.Abs(candidate.X - x);
                if (!candidate.CanInteract || distance > rangeX || Math.Abs(candidate.Y - y) > rangeY || distance >= bestDistance) continue;
                best = i; bestDistance = distance;
            }
            return best;
        }
    }
}
