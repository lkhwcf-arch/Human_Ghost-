using System;

namespace HumanGhost.Gameplay
{
    // 게임 속도를 한 곳에서 정합니다(O01_2 #7). 우선순위는 일시정지 > 히트스톱 > 슬로입니다. 시간은 멈추지 않는 ms로 받습니다.
    public sealed class GameSpeedState
    {
        private bool _paused;
        private int _hitStop;
        private int _slow;
        private float _slowRate = 1f;
        private int _queuedSlow;
        private float _queuedRate = 1f;

        public float Scale => _paused || _hitStop > 0 ? 0f : _slow > 0 ? _slowRate : 1f;
        public bool IsPaused => _paused;

        public void SetPaused(bool paused) => _paused = paused;

        public void HitStop(int milliseconds)
        {
            if (milliseconds < 0) throw new ArgumentOutOfRangeException(nameof(milliseconds));
            _hitStop = Math.Max(_hitStop, milliseconds);
        }

        // 치명타: 멈춤 뒤 슬로(C11). 해체 등 더 긴 멈춤과 겹치면 멈춤이 끝난 뒤 이어집니다.
        public void Critical(int stopMilliseconds, int slowMilliseconds, float rate)
        {
            if (slowMilliseconds < 0 || rate <= 0f || rate > 1f) throw new ArgumentOutOfRangeException(nameof(rate));
            HitStop(stopMilliseconds);
            _queuedSlow = slowMilliseconds; _queuedRate = rate;
        }

        // 해체 연출에는 치명 슬로를 겹치지 않습니다(C11).
        public void ClearSlow() { _slow = 0; _queuedSlow = 0; }

        public void Tick(int milliseconds)
        {
            if (milliseconds < 0) throw new ArgumentOutOfRangeException(nameof(milliseconds));
            if (_paused) return;
            if (_hitStop > 0)
            {
                int used = Math.Min(_hitStop, milliseconds);
                _hitStop -= used; milliseconds -= used;
                if (_hitStop > 0) return;
            }
            if (_queuedSlow > 0) { _slow = _queuedSlow; _slowRate = _queuedRate; _queuedSlow = 0; }
            _slow = Math.Max(0, _slow - milliseconds);
        }
    }
}
