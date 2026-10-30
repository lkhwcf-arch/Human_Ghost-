using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HumanGhost.Data.Csv;
using UnityEngine;

namespace HumanGhost.Data.Infrastructure
{
    // 에셋은 게임 엔진이 소유하며 이 입력 객체는 참조만 보관합니다. 주 실행 흐름에서 사용합니다.
    public sealed class TextAssetCollectionSource : ICsvTextSource
    {
        private readonly Dictionary<string, TextAsset> _assets = new Dictionary<string, TextAsset>();
        private static readonly Encoding _encoding = new UTF8Encoding(false, true);

        public TextAssetCollectionSource(IEnumerable<TextAsset> csvFiles, IEnumerable<TextAsset> jsonFiles)
        {
            Add(csvFiles, "Csv", ".csv");
            Add(jsonFiles, "Json", ".json");
        }

        public Task<string> ReadTextAsync(string sourceName, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (!_assets.TryGetValue(sourceName.Replace('\\', '/'), out TextAsset asset) || asset == null)
                throw new FileNotFoundException($"데이터 텍스트 에셋을 찾을 수 없습니다: {sourceName}");
            return Task.FromResult(_encoding.GetString(asset.bytes));
        }

        private void Add(IEnumerable<TextAsset> files, string folder, string extension)
        {
            foreach (TextAsset asset in files)
            {
                if (asset == null) throw new FileNotFoundException("데이터 에셋 참조가 없습니다. 에디터에서 데이터 연결을 갱신해 주세요.");
                _assets.Add(folder + "/" + asset.name + extension, asset);
            }
        }
    }
}
