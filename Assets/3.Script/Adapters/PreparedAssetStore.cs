using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HumanGhost.Gameplay.Adapters
{
    // 한 준비 수명이 리소스를 소유합니다. 늦은 완료는 Unity 객체를 호출하지 않고 반환만 합니다.
    public class PreparedAssetStore<T> : IDisposable where T : class
    {
        private sealed class Entry
        {
            public Task Loading;
            public AssetLease<T> Lease;
        }
        private readonly object _gate = new object();
        private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly IAssetSource<T> _source;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private bool _disposed;

        public PreparedAssetStore(IAssetSource<T> source) => _source = source ?? throw new ArgumentNullException(nameof(source));

        public async Task PrepareAsync(IEnumerable<string> addresses, CancellationToken cancellation = default)
        {
            if (addresses == null) throw new ArgumentNullException(nameof(addresses));
            cancellation.ThrowIfCancellationRequested();
            foreach (string address in new List<string>(addresses))
            {
                cancellation.ThrowIfCancellationRequested();
                Task loading;
                lock (_gate)
                {
                    RequireAlive();
                    if (!_entries.TryGetValue(address, out Entry entry))
                    {
                        entry = new Entry();
                        _entries.Add(address, entry);
                        entry.Loading = LoadAsync(address, entry, _lifetime.Token);
                    }
                    loading = entry.Loading;
                }
                await loading;
                cancellation.ThrowIfCancellationRequested();
                lock (_gate) RequireAlive();
            }
            lock (_gate) RequireAlive();
        }

        public bool TryGet(string address, out T asset)
        {
            lock (_gate)
            {
                asset = !_disposed && _entries.TryGetValue(address, out Entry entry) ? entry.Lease?.Asset : null;
                return asset != null;
            }
        }

        private async Task LoadAsync(string address, Entry entry, CancellationToken cancellation)
        {
            AssetLease<T> lease = null;
            try
            {
                lease = await _source.LoadAsync(address, cancellation);
                if (lease == null) throw new InvalidOperationException("리소스 공급자가 반환한 준비 결과가 비어 있습니다.");
                lock (_gate)
                {
                    RequireAlive();
                    entry.Lease = lease;
                    lease = null;
                }
            }
            catch (Exception error)
            {
                bool ended;
                lock (_gate)
                {
                    ended = _disposed;
                    if (_entries.TryGetValue(address, out Entry current) && ReferenceEquals(current, entry)) _entries.Remove(address);
                }
                if (ended) throw new OperationCanceledException("종료된 수명의 리소스 준비 결과입니다.", error);
                throw;
            }
            finally { lease?.Dispose(); }
        }

        private void RequireAlive()
        {
            if (_disposed) throw new OperationCanceledException("이미 종료된 리소스 준비 수명입니다.");
        }

        public void Dispose()
        {
            var leases = new List<AssetLease<T>>();
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                foreach (Entry entry in _entries.Values) if (entry.Lease != null) leases.Add(entry.Lease);
                _entries.Clear();
            }
            _lifetime.Cancel();
            _lifetime.Dispose();
            foreach (AssetLease<T> lease in leases) lease.Dispose();
        }
    }
}
