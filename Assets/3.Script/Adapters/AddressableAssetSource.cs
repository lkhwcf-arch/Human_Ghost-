using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace HumanGhost.Gameplay.Adapters
{
    // 주소 로딩과 핸들 반환만 담당합니다. 씬·소리·게임 규칙은 소유하지 않습니다.
    public class AddressableAssetSource<T> : IAssetSource<T> where T : UnityEngine.Object
    {
        public async Task<AssetLease<T>> LoadAsync(string address, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            var handle = Addressables.LoadAssetAsync<T>(address);
            try
            {
                T asset = await handle.Task;
                cancellation.ThrowIfCancellationRequested();
                if (handle.Status != AsyncOperationStatus.Succeeded || asset == null)
                    throw new InvalidOperationException("리소스를 읽지 못했습니다. 주소: " + address, handle.OperationException);
                return new AssetLease<T>(asset, () => { if (handle.IsValid()) Addressables.Release(handle); });
            }
            catch (Exception error)
            {
                if (handle.IsValid()) Addressables.Release(handle);
                if (error is OperationCanceledException) throw;
                throw new InvalidOperationException("등록된 주소에서 리소스를 읽지 못했습니다. 주소: " + address, error);
            }
        }
    }
}
