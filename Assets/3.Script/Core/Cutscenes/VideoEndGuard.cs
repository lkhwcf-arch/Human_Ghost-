using System;

namespace HumanGhost.Gameplay
{
    // Decoder completion fallback. Mid-video stalls, loops and pauses never finish a video.
    public sealed class VideoEndGuard
    {
        private const int _holdMilliseconds = 150;
        private int _lastFrameMilliseconds;
        public void Reset() => _lastFrameMilliseconds = 0;
        public bool Step(bool prepared, bool paused, bool looping, bool atLastFrame, int elapsedMilliseconds)
        {
            if (elapsedMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(elapsedMilliseconds));
            bool lastFrame = prepared && !paused && !looping && atLastFrame;
            if (!lastFrame) { Reset(); return false; }
            _lastFrameMilliseconds += Math.Min(elapsedMilliseconds, _holdMilliseconds - _lastFrameMilliseconds);
            return _lastFrameMilliseconds == _holdMilliseconds;
        }
    }
}
