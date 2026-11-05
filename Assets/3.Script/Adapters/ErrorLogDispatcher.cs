using System;
using System.Threading;

namespace HumanGhost.Gameplay.Adapters
{
    public readonly struct ErrorLogMessage
    {
        public string Kind { get; }
        public string Message { get; }
        public string Stack { get; }
        public DateTimeOffset Time { get; }
        public int Count { get; }
        public ErrorLogMessage(string kind, string message, string stack, DateTimeOffset time, int count)
        { Kind = kind; Message = message; Stack = stack; Time = time; Count = count; }
    }

    // 많은 로그를 최신 내용·누적 수로 합쳐 한 번의 메인 스레드 전달만 예약합니다.
    public class ErrorLogDispatcher : IDisposable
    {
        private readonly object _gate = new object();
        private readonly SynchronizationContext _context;
        private readonly Action<ErrorLogMessage> _receive;
        private ErrorLogMessage _pending;
        private bool _scheduled, _disposed;
        public ErrorLogDispatcher(SynchronizationContext context, Action<ErrorLogMessage> receive)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _receive = receive ?? throw new ArgumentNullException(nameof(receive));
        }
        public virtual void Post(string kind, string message, string stack, DateTimeOffset time)
        {
            lock (_gate)
            {
                if (_disposed) return;
                _pending = new ErrorLogMessage(kind, message, stack, time, _pending.Count + 1);
                if (_scheduled) return;
                _scheduled = true;
            }
            _context.Post(_ => Drain(), null);
        }
        private void Drain()
        {
            ErrorLogMessage message;
            lock (_gate)
            {
                if (_disposed) return;
                message = _pending;
                _pending = default;
                _scheduled = false;
            }
            _receive(message);
        }
        public virtual void Dispose()
        {
            lock (_gate) { _disposed = true; _pending = default; }
        }
    }
}
