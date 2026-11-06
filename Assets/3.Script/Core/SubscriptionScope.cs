using System;
using System.Collections.Generic;

namespace HumanGhost.Gameplay
{
    // 연결 수명의 구독과 지연 표시 예약을 함께 정리합니다. 전역 이벤트를 소유하지 않습니다.
    public class SubscriptionScope : IDisposable
    {
        private readonly List<Action> _unsubscribe = new List<Action>();
        private bool _disposed;
        private bool _pending;
        public bool IsDisposed => _disposed;
        public void Add(Action unsubscribe)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SubscriptionScope));
            _unsubscribe.Add(unsubscribe ?? throw new ArgumentNullException(nameof(unsubscribe)));
        }
        public void Invalidate() { if (!_disposed) _pending = true; }
        // 이미 복사된 호출 목록의 콜백도 이전 수명이 끝났으면 실행하지 않습니다.
        public Action Guard(Action callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            return () => { if (!_disposed) callback(); };
        }
        public Action<T> Guard<T>(Action<T> callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            return value => { if (!_disposed) callback(value); };
        }
        public bool TryConsumeRefresh()
        {
            if (_disposed || !_pending) return false;
            _pending = false;
            return true;
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true; _pending = false;
            List<Exception> failures = null;
            for (int i = _unsubscribe.Count - 1; i >= 0; i--)
            {
                try { _unsubscribe[i](); }
                catch (Exception error) { (failures ??= new List<Exception>()).Add(error); }
            }
            _unsubscribe.Clear();
            if (failures != null) throw new AggregateException("구독 해제 중 오류가 발생했습니다.", failures);
        }
    }
}
