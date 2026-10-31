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
        private readonly Action<string, Exception> _reportConsumerError;
        private Task<IReadOnlyDataCatalog> _initialization;

        public event Action<IReadOnlyDataCatalog> Initialized;
        public IReadOnlyDataCatalog Data { get; private set; }

        public DataBootstrapper(ICsvTextSource source, Func<string, string> resolvePath, Action<string, Exception> reportConsumerError = null)
        {
            _tableLoader = new BootstrapTableLoader(new CsvLoader(source, new CsvParser()), source, resolvePath);
            _reportConsumerError = reportConsumerError;
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
            DataReadyNotification.Publish(Initialized, catalog, _reportConsumerError);
            return catalog;
        }

        private Task<DataCatalog> LoadCsvAsync(CancellationToken token)
            => GeneratedDataLoadPlan.LoadCsvAsync(_tableLoader, new DataCatalog(), token);

        private Task<DataCatalog> LoadJsonAsync(DataCatalog catalog, CancellationToken token)
            => GeneratedDataLoadPlan.LoadJsonAsync(_tableLoader, catalog, token);
    }

    // 데이터 완료 통지에만 적용합니다. 명령이나 필수 초기화 순서를 대신하지 않습니다.
    internal static class DataReadyNotification
    {
        public static void Publish<T>(Action<T> consumers, T value, Action<string, Exception> reportError)
        {
            if (consumers == null) return;
            // 호출 시작 시 구독 목록을 고정하고 구독 순서대로 각각 한 번씩 통지합니다.
            foreach (Action<T> consumer in consumers.GetInvocationList())
            {
                try { consumer(value); }
                catch (Exception error)
                {
                    string name = $"{consumer.Method.DeclaringType?.FullName}.{consumer.Method.Name}";
                    try
                    {
                        if (reportError != null) reportError(name, error);
                        else System.Diagnostics.Trace.TraceError($"데이터 완료 소비자 처리에 실패했습니다. 소비자: {name}\n{error}");
                    }
                    catch (Exception reportingError)
                    {
                        // 오류 기록자의 실패도 다음 소비자와 데이터 준비 결과에 전파하지 않습니다.
                        System.Diagnostics.Trace.TraceError($"데이터 소비자 오류 기록에 실패했습니다. 소비자: {name}\n원래 오류: {error}\n기록 오류: {reportingError}");
                    }
                }
            }
        }
    }
}
