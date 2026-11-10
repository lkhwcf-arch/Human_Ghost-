using System;

namespace HumanGhost.Gameplay.Presentation
{
    // 표시 대상과 구독 수명만 소유합니다. 피해 계산·Unity 오브젝트는 소유하지 않습니다.
    public sealed class HealthDisplayBinding : IDisposable
    {
        private readonly Action<int, int> _show;
        private PlayerActor _source;
        private Action<int> _handler;
        private long _generation;
        private bool _active;
        private bool _disposed;

        public bool IsBound => _source != null;

        public HealthDisplayBinding(Action<int, int> show)
        {
            _show = show ?? throw new ArgumentNullException(nameof(show));
        }

        public void Bind(PlayerActor source)
        {
            RequireAlive();
            if (source == null) throw new ArgumentNullException(nameof(source));
            Disconnect();
            _source = source;
            if (_active) Connect();
        }

        public void SetActive(bool active)
        {
            RequireAlive();
            if (_active == active) return;
            Disconnect();
            _active = active;
            if (_active) Connect();
        }

        // 비활성화는 같은 대상의 재연결을 허용하지만 반환·대상 해제는 참조도 지웁니다.
        public void Unbind()
        {
            RequireAlive();
            Disconnect();
            _source = null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            Disconnect();
            _source = null;
            _active = false;
            _disposed = true;
        }

        private void Connect()
        {
            if (_source == null) return;
            PlayerActor source = _source;
            long generation = _generation;
            _handler = value =>
            {
                // 이벤트 호출 목록에 남아 있어도 이전 대상·이전 활성 주기의 결과는 적용하지 않습니다.
                if (_active && generation == _generation && ReferenceEquals(_source, source))
                    _show(source.Health, source.MaxHealth);
            };
            source.HealthChanged += _handler;
            try { _show(source.Health, source.MaxHealth); }
            catch
            {
                Disconnect();
                _active = false;
                throw;
            }
        }

        private void Disconnect()
        {
            _generation++;
            if (_source != null && _handler != null) _source.HealthChanged -= _handler;
            _handler = null;
        }

        private void RequireAlive()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(HealthDisplayBinding), "종료한 체력 표시는 다시 연결할 수 없습니다.");
        }
    }
}
