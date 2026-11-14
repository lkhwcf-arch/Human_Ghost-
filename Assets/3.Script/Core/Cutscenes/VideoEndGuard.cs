using System;

namespace HumanGhost.Gameplay
{
    // Decoder completion fallback. Mid-video stalls, loops and pauses never finish a video.
    public sealed class VideoEndGuard
    {
        private const int _holdMilliseconds = 150;
        private int _lastFrameMilliseconds;
        public void Reset() => _lastFrameMilliseconds = 0;
        public bool Step(bool prepared, bool paused, bool looping, long frame, ulong frameCount,
            double time, double length, double frameRate, int elapsedMilliseconds)
        {
            if (elapsedMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(elapsedMilliseconds));
            bool lastFrame = prepared && !paused && !looping && frame >= 0 && frameCount > 0 &&
                (ulong)frame >= frameCount - 1 && length > 0 && !double.IsInfinity(length) &&
                frameRate > 0 && !double.IsInfinity(frameRate) && time >= length - 1d / frameRate - .001;
            if (!lastFrame) { Reset(); return false; }
            _lastFrameMilliseconds += Math.Min(elapsedMilliseconds, _holdMilliseconds - _lastFrameMilliseconds);
            return _lastFrameMilliseconds == _holdMilliseconds;
        }
    }
}
