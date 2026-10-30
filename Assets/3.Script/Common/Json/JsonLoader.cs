using System;
using System.Threading;
using System.Threading.Tasks;
using HumanGhost.Data.Text;

namespace HumanGhost.Data.Json
{
    // 기존 유니코드 파일·텍스트 에셋 입력을 재사용하고 객체 변환 방법은 외부에서 전달받습니다.
    public sealed class JsonLoader<T>
    {
        private readonly ITextSource _source;
        private readonly Func<string, T> _read;

        public JsonLoader(ITextSource source, Func<string, T> read)
        {
            _source = source;
            _read = read;
        }

        public async Task<T> LoadAsync(string sourceName, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            string text = await _source.ReadTextAsync(sourceName, token);
            token.ThrowIfCancellationRequested();
            T result = _read(text);
            token.ThrowIfCancellationRequested();
            return result;
        }
    }
}
