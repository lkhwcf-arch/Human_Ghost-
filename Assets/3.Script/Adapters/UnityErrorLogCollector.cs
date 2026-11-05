using System;
using System.Threading;
using UnityEngine;

namespace HumanGhost.Gameplay.Adapters
{
    // Unity 로그 수집만 담당합니다. 매 프레임 조회하거나 작업 스레드에서 UI를 만지지 않습니다.
    public class UnityErrorLogCollector : IDisposable
    {
        private readonly ErrorLogDispatcher _dispatcher;
        private readonly Action<ErrorLogMessage> _receive;
        private int _disposed, _delivering;
        public bool IsActive => Volatile.Read(ref _disposed) == 0;
        public UnityErrorLogCollector(Action<ErrorLogMessage> receive)
        {
            _receive = receive ?? throw new ArgumentNullException(nameof(receive));
            _dispatcher = new ErrorLogDispatcher(SynchronizationContext.Current, Deliver);
            Application.logMessageReceivedThreaded += Capture;
        }
        private void Capture(string message, string stack, LogType type)
        {
            if (Volatile.Read(ref _disposed) != 0 || Volatile.Read(ref _delivering) != 0 || ErrorPopupHost.IsWritingReport) return;
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            _dispatcher.Post(type.ToString(), message, stack, DateTimeOffset.Now);
        }
        private void Deliver(ErrorLogMessage message)
        {
            if (Volatile.Read(ref _disposed) != 0) return;
            Interlocked.Exchange(ref _delivering, 1);
            try { _receive(message); }
            catch (Exception error)
            {
                // 표시 실패는 원본 로그에 남기고 수집을 끊어 자기 오류로 팝업을 계속 만들지 않습니다.
                Dispose();
                Debug.LogException(error);
            }
            finally { Interlocked.Exchange(ref _delivering, 0); }
        }
        public virtual void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            Application.logMessageReceivedThreaded -= Capture;
            _dispatcher.Dispose();
        }
    }
}
