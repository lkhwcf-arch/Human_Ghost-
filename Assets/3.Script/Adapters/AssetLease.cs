using System;
using System.Threading;
using System.Threading.Tasks;

namespace HumanGhost.Gameplay.Adapters
{
    // 사용이 끝난 리소스는 소유자가 한 번만 반환합니다. 원본 에셋은 수정하지 않습니다.
    public class AssetLease<T> : IDisposable where T : class
    {
        private Action _release;
        public T Asset { get; }
        public AssetLease(T asset, Action release)
        {
            Asset = asset ?? throw new ArgumentNullException(nameof(asset));
            _release = release ?? throw new ArgumentNullException(nameof(release));
        }
        public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
    }

    public interface IAssetSource<T> where T : class
    {
        Task<AssetLease<T>> LoadAsync(string address, CancellationToken cancellation);
    }
}
