using System;
using System.Threading;
using System.Threading.Tasks;
using HumanGhost.Data.Csv;
using HumanGhost.Data.Storage;

namespace HumanGhost.Data.Infrastructure
{
    // 이 객체의 소유자가 최초 호출의 취소 토큰과 완료 이벤트 구독 수명을 관리합니다.
    public sealed class DataBootstrapper
    {
        private readonly BootstrapTableLoader _tableLoader;
        private Task<IReadOnlyDataCatalog> _initialization;

        public event Action<IReadOnlyDataCatalog> Initialized;
        public IReadOnlyDataCatalog Data { get; private set; }

        public DataBootstrapper(ICsvTextSource source, Func<string, string> resolvePath)
        {
            _tableLoader = new BootstrapTableLoader(new CsvLoader(source, new CsvParser()), source, resolvePath);
        }

        // 재호출은 같은 초기화 작업을 공유합니다. 실패 후 재시도는 새 초기화 객체로 수행합니다.
        public Task<IReadOnlyDataCatalog> InitializeAsync(CancellationToken token)
            => _initialization ??= InitializeCoreAsync(token);

        private async Task<IReadOnlyDataCatalog> InitializeCoreAsync(CancellationToken token)
        {
            // 작업 참조를 먼저 보관하여 완료 이벤트에서 재호출해도 같은 작업을 반환합니다.
            await Task.Yield();
            var sequence = new DataLoadSequence<DataCatalog, DataCatalog>(LoadCsvAsync, LoadJsonAsync);
            DataCatalog catalog = await sequence.LoadAsync(token);
            Data = catalog;
            Initialized?.Invoke(catalog);
            return catalog;
        }

        private Task<DataCatalog> LoadCsvAsync(CancellationToken token)
            => GeneratedDataLoadPlan.LoadCsvAsync(_tableLoader, new DataCatalog(), token);

        private Task<DataCatalog> LoadJsonAsync(DataCatalog catalog, CancellationToken token)
            => GeneratedDataLoadPlan.LoadJsonAsync(_tableLoader, catalog, token);
    }
}
