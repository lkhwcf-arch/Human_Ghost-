using System;

namespace HumanGhost.Gameplay
{
    // 판정이 소유한 선딜·판정·후딜을 표시 프레임에 대응합니다. Unity나 상태 ID에 의존하지 않습니다.
    public static class AttackAnimationTiming
    {
        // endFrame은 다음 구간의 시작(미포함). 마지막 프레임에서 멈춥니다.
        public static double FrameAt(AttackSpec attack, int elapsed, int firstFrame, int contactFrame, int recoveryFrame, int endFrame)
        {
            if (attack == null) throw new ArgumentNullException(nameof(attack));
            if (elapsed < 0) throw new ArgumentOutOfRangeException(nameof(elapsed));
            if (firstFrame < 0 || contactFrame < firstFrame || recoveryFrame <= contactFrame || endFrame < recoveryFrame)
                throw new ArgumentOutOfRangeException(nameof(contactFrame), "공격 표시 프레임 순서를 확인하세요.");
            double frame;
            if (elapsed < attack.Windup)
                frame = firstFrame + (contactFrame - firstFrame) * (double)elapsed / attack.Windup;
            else if (elapsed < attack.Windup + attack.Active)
                frame = contactFrame + (recoveryFrame - contactFrame) * (double)(elapsed - attack.Windup) / attack.Active;
            else
                frame = attack.Recovery == 0 ? endFrame - 1 : recoveryFrame +
                    (endFrame - recoveryFrame) * (double)Math.Min(elapsed - attack.Windup - attack.Active, attack.Recovery) / attack.Recovery;
            return Math.Min(frame, endFrame - 1);
        }
    }
}
