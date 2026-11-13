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
            await loader.LoadCsvAsync<CorruptionStep>(catalog, row => row.ID, token);
            await loader.LoadCsvAsync<Currency>(catalog, row => row.ID, token);
            await loader.LoadCsvAsync<Cutscene>(catalog, row => row.ID, token);
            await loader.LoadCsvAsync<CutsceneStep>(catalog, row => row.ID, token);
            await loader.LoadCsvAsync<Dialogue>(catalog, row => row.ID, token);
            await loader.LoadCsvAsync<EnumDefinition>(catalog, row => row.ID, token);
            await loader.LoadCsvAsync<ExecuteReward>(catalog, row => row.ID, token);
            await loader.LoadCsvAsync<FieldObject>(catalog, row => row.ID, token);
            await loader.LoadCsvAsync<FieldObjectPlacement>(catalog, row => row.ID, token);
            await loader.LoadCsvAsync<GateConfig>(catalog, row => row.ID, token);
            await loader.LoadCsvAsync<HealthDisplayConfig>(catalog, row => row.ID, token);
            await loader.LoadCsvAsync<Language>(catalog, row => row.ID, token);
            await loader.LoadCsvAsync<MonsterReward>(catalog, row => row.ID, token);
            await loader.LoadCsvAsync<MonsterSkill>(catalog, row => row.ID, token);
            await loader.LoadCsvAsync<Npc>(catalog, row => row.ID, token);
            await loader.LoadCsvAsync<PlayerAction>(catalog, row => row.ID, token);
            await loader.LoadCsvAsync<PlayerLevel>(catalog, row => row.ID, token);
            await loader.LoadCsvAsync<Popup>(catalog, row => row.ID, token);
            await loader.LoadCsvAsync<Skill>(catalog, row => row.ID, token);
            await loader.LoadCsvAsync<Sound>(catalog, row => row.ID, token);
            await loader.LoadCsvAsync<Stage>(catalog, row => row.ID, token);
            await loader.LoadCsvAsync<StageLayer>(catalog, row => row.ID, token);
            await loader.LoadCsvAsync<State>(catalog, row => row.ID, token);
            await loader.LoadCsvAsync<StatusEffect>(catalog, row => row.ID, token);
            await loader.LoadCsvAsync<UI_Lang>(catalog, row => row.ID, token);
            await loader.LoadCsvAsync<Upgrade>(catalog, row => row.ID, token);
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
            await loader.LoadGroupsAsync<MonsterState>(catalog, token, "UnitID");
            await loader.LoadGroupsAsync<PlayerState>(catalog, token, "UnitID");
            var shrineReader = new HumanGhost.Data.Mapping.ShrineDefinitionReader();
            var shrineLevel = await loader.ReadJsonAsync("shrine_level", shrineReader.ReadLevel, token);
            var shrineStats = new System.Collections.Generic.List<ShrineStatDefinition>();
            shrineStats.Add(await loader.ReadJsonAsync("AttackSpeed", shrineReader.ReadStat, token));
            shrineStats.Add(await loader.ReadJsonAsync("Crit", shrineReader.ReadStat, token));
            shrineStats.Add(await loader.ReadJsonAsync("Dodge", shrineReader.ReadStat, token));
            shrineStats.Add(await loader.ReadJsonAsync("ParryStamina", shrineReader.ReadStat, token));
            shrineStats.Add(await loader.ReadJsonAsync("StaminaRegen", shrineReader.ReadStat, token));
            catalog.RegisterDefinition(new ShrineDefinitions(shrineLevel, shrineStats));
            var galimgilReader = new HumanGhost.Data.Mapping.GalimgilDefinitionReader();
            catalog.RegisterDefinition(await loader.ReadJsonAsync("galimgil_reward", galimgilReader.Read, token));
            await Task.CompletedTask;
            return catalog;
        }
    }
}
