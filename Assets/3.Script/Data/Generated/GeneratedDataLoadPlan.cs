// 시트 규칙에서 자동 생성한 로딩 계획입니다. 직접 수정하지 마세요.
using System.Threading;
using System.Threading.Tasks;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Storage;

namespace HumanGhost.Data.Infrastructure
{
    internal static class GeneratedDataLoadPlan
    {
        public static async Task<DataCatalog> LoadCsvAsync(BootstrapTableLoader loader, DataCatalog catalog, CancellationToken token)
        {
            await loader.LoadCsvAsync<UnitStat>(catalog, row => row.ID, token);
            await loader.LoadCsvAsync<Buff>(catalog, row => row.ID, token);
            await loader.LoadCsvAsync<Language>(catalog, row => row.ID, token);
            await loader.LoadCsvAsync<Skill>(catalog, row => row.ID, token);
            await loader.LoadCsvAsync<Stage>(catalog, row => row.ID, token);
            await loader.LoadCsvAsync<StageLayer>(catalog, row => row.ID, token);
            await loader.LoadCsvAsync<State>(catalog, row => row.ID, token);
            await loader.LoadCsvAsync<Monster>(catalog, row => row.ID, token);
            await loader.LoadCsvAsync<Player>(catalog, row => row.ID, token);
            await Task.CompletedTask;
            return catalog;
        }
        public static async Task<DataCatalog> LoadJsonAsync(BootstrapTableLoader loader, DataCatalog catalog, CancellationToken token)
        {
            var reader = new HumanGhost.Data.Mapping.JsonTableReader();
            await loader.LoadCharactersAsync<BossState, Boss>(catalog, (common, states) => new Boss(common, states), row => row.ID, token, "UnitID");
            await loader.LoadCharactersAsync<EliteState, Elite>(catalog, (common, states) => new Elite(common, states), row => row.ID, token, "UnitID");
            await loader.LoadJsonAsync(catalog, "Item", reader.Read<Item>, row => row.ID, token);
            await loader.LoadGroupsAsync<PlayerState>(catalog, token, "UnitID");
            await Task.CompletedTask;
            return catalog;
        }
    }
}
