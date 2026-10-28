using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HumanGhost.Data.Csv;
using UnityEngine;

namespace HumanGhost.Data.Infrastructure
{
    // TextAsset 접근은 Unity 메인 스레드에서 수행합니다. 파싱에는 Unity 의존성이 없습니다.
    public sealed class UnityTextAssetCsvSource : ICsvTextSource
    {
        private readonly TextAsset _asset;
        public UnityTextAssetCsvSource(TextAsset asset)
        {
            _asset = asset != null ? asset : throw new ArgumentNullException(nameof(asset));
        }

        public Task<string> ReadTextAsync(string sourceName, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (_asset == null)
                throw new InvalidOperationException("CSV TextAsset이 제거되었습니다.");
            return Task.FromResult(new UTF8Encoding(false, true).GetString(_asset.bytes));
        }
    }
}
