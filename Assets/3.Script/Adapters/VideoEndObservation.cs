using System;

namespace HumanGhost.Gameplay.Adapters
{
    // VideoPlayer의 실수 초·프레임률은 이 관측 경계에서만 읽고 코어에는 마지막 프레임 여부를 전달합니다.
    public static class VideoEndObservation
    {
        public static bool IsLastFrame(long frame, ulong frameCount, double time, double length, double frameRate)
        {
            return frame >= 0 && frameCount > 0 && (ulong)frame >= frameCount - 1 &&
                length > 0 && !double.IsInfinity(length) && frameRate > 0 && !double.IsInfinity(frameRate) &&
                !double.IsNaN(time) && !double.IsInfinity(time) && time >= length - 1d / frameRate - .001;
        }
    }
}
