using System;
using System.Threading;
using System.Threading.Tasks;

namespace HumanGhost.Data.Infrastructure
{
    // 표 데이터의 파일 읽기와 객체 변환을 모두 마친 결과만 구조화 데이터 로딩 단계에 전달합니다.
    public sealed class DataLoadSequence<TCsv, TResult>
    {
        private readonly Func<CancellationToken, Task<TCsv>> _loadCsv;
        private readonly Func<TCsv, CancellationToken, Task<TResult>> _loadJson;

        public DataLoadSequence(Func<CancellationToken, Task<TCsv>> loadCsv,
            Func<TCsv, CancellationToken, Task<TResult>> loadJson)
        {
            _loadCsv = loadCsv ?? throw new ArgumentNullException(nameof(loadCsv), "CSV 로딩 단계가 필요합니다.");
            _loadJson = loadJson ?? throw new ArgumentNullException(nameof(loadJson), "JSON 로딩 단계가 필요합니다.");
        }

        public async Task<TResult> LoadAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            TCsv csv = await _loadCsv(token);
            token.ThrowIfCancellationRequested();
            TResult result = await _loadJson(csv, token);
            token.ThrowIfCancellationRequested();
            return result;
        }
    }
}
