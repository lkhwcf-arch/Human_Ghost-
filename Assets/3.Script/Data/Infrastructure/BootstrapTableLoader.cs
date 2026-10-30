using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HumanGhost.Data.Csv;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Json;
using HumanGhost.Data.Mapping;
using HumanGhost.Data.Storage;
using HumanGhost.Data.Text;

namespace HumanGhost.Data.Infrastructure
{
    // 파일 형식별 공통 로딩만 담당하고 초기화 순서는 관리하지 않습니다.
    internal sealed class BootstrapTableLoader
    {
        private readonly CsvLoader _csvLoader;
        private readonly ITextSource _source;
        private readonly Func<string, string> _resolvePath;

        public BootstrapTableLoader(CsvLoader csvLoader, ITextSource source, Func<string, string> resolvePath)
        {
            _csvLoader = csvLoader;
            _source = source;
            _resolvePath = resolvePath ?? throw new ArgumentNullException(nameof(resolvePath), "데이터 경로 연결 함수가 필요합니다.");
        }

        public async Task LoadCsvAsync<T>(DataCatalog catalog, Func<T, int> selectId, CancellationToken token)
        {
            CsvDocument document = await _csvLoader.LoadAsync(_resolvePath(Path.Combine("Csv", typeof(T).Name + ".csv")), token);
            var mapper = new CsvTableMapper<T>(new CsvDefinitionMapper<T>(id => catalog.GetTable<UnitStat>().Get(id)));
            catalog.Register(new DataTable<T>(mapper.Map(document), selectId));
        }

        public async Task<IReadOnlyDataTable<T>> LoadJsonAsync<T>(DataCatalog catalog, string name,
            Func<string, IReadOnlyList<T>> read, Func<T, int> selectId, CancellationToken token)
        {
            var loader = new JsonLoader<IReadOnlyList<T>>(_source, read);
            IReadOnlyList<T> definitions = await loader.LoadAsync(_resolvePath(Path.Combine("Json", name + ".json")), token);
            var table = new DataTable<T>(definitions, selectId);
            catalog.Register(table);
            return table;
        }

        public Task<IReadOnlyDataTable<UnitStateGroup<T>>> LoadGroupsAsync<T>(DataCatalog catalog, CancellationToken token)
            => LoadJsonAsync(catalog, typeof(T).Name, new JsonTableReader().ReadGroups<T>, group => group.UnitID, token);
    }
}
