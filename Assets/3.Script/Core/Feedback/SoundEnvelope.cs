using System;

namespace HumanGhost.Gameplay
{
    // 표시용 음량의 입출력 곡선입니다. 정수 밀리세컨드와 10000분율을 사용합니다.
    public class SoundEnvelope
    {
        private readonly int _fadeIn, _fadeOut;
        private long _elapsed;
        private int _stopGain;
        private bool _stopping;
        public int Gain { get; private set; }
        public bool IsStopped => _stopping && Gain == 0;
        public bool IsStopping => _stopping;
        public SoundEnvelope(int fadeInMs, int fadeOutMs)
        {
            if (fadeInMs < 0 || fadeOutMs < 0) throw new ArgumentOutOfRangeException(nameof(fadeInMs));
            _fadeIn = fadeInMs; _fadeOut = fadeOutMs;
            Gain = fadeInMs == 0 ? 10000 : 0;
        }
        public void Stop()
        {
            if (_stopping) return;
            _stopping = true; _elapsed = 0; _stopGain = Gain;
            if (_fadeOut == 0) Gain = 0;
        }
        public void Tick(int milliseconds)
        {
            if (milliseconds < 0) throw new ArgumentOutOfRangeException(nameof(milliseconds));
            _elapsed += milliseconds;
            if (_stopping) Gain = _fadeOut == 0 ? 0 : (int)(_stopGain * Math.Max(0L, _fadeOut - _elapsed) / _fadeOut);
            else Gain = _fadeIn == 0 ? 10000 : (int)(10000L * Math.Min(_elapsed, _fadeIn) / _fadeIn);
        }
    }
}
