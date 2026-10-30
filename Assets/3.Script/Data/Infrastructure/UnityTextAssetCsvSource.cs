using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HumanGhost.Data.Csv;
using UnityEngine;

namespace HumanGhost.Data.Infrastructure
{
    // 텍스트 에셋은 게임 엔진의 주 실행 흐름에서 읽습니다. 문자열 해석은 게임 엔진에 의존하지 않습니다.
    public sealed class UnityTextAssetCsvSource : ICsvTextSource
    {
        private readonly TextAsset _asset;
        public UnityTextAssetCsvSource(TextAsset asset)
        {
            _asset = asset != null ? asset : throw new ArgumentNullException(nameof(asset), "읽을 텍스트 에셋이 필요합니다.");
        }

        public Task<string> ReadTextAsync(string sourceName, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (_asset == null)
                throw new InvalidOperationException("CSV 텍스트 에셋이 제거되었습니다.");
            return Task.FromResult(new UTF8Encoding(false, true).GetString(_asset.bytes));
        }
    }
}
