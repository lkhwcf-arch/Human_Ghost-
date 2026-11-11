using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Infrastructure;
using HumanGhost.Data.Json;
using HumanGhost.Data.Mapping;
using HumanGhost.Data.Storage;
using Newtonsoft.Json.Linq;

internal static class ShrineDefinitionChecks
{
    public static async Task RunAsync(Action<bool> check)
    {
        var reader = new ShrineDefinitionReader();
        string path = Path.Combine(AppContext.BaseDirectory, "shrine_level.json");
        var level = await new JsonLoader<ShrineLevelDefinition>(new Utf8CsvFileSource(), reader.ReadLevel)
            .LoadAsync(path, CancellationToken.None);
        var levelSource = JObject.Parse(File.ReadAllText(path));
        check(level.MaxLevel == level.SoulCosts.Count && level.SoulCurrencyId == (int)levelSource["soulCurrencyId"]
            && level.SoulCostBase > 0 && level.SoulCostRate >= 10000);
        string statText = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ParryStamina.json"));
        var reduction = reader.ReadStat(statText);
        var statSource = JObject.Parse(statText);
        check(reduction.StatType == (string)statSource["statType"] && reduction.MaxStep == reduction.Steps.Count &&
            statSource["steps"].All(step => reduction.Steps[(int)step["step"]] == (int)step["value"]));
        var inputSteps = new Dictionary<int, int> { [1] = 5, [2] = 10 };
        var independent = new ShrineStatDefinition("Example", 1, "검사 자료", true, 2, inputSteps);
        inputSteps[1] = 999;
        check(independent.Steps[1] == 5);
        bool readOnly = false;
        try { ((IDictionary<int, int>)independent.Steps)[1] = 100; }
        catch (NotSupportedException) { readOnly = true; }
        check(readOnly);
        var inputs = new List<ShrineStatDefinition> { independent, reduction };
        var definitions = new ShrineDefinitions(level, inputs);
        inputs.Clear();
        check(definitions.Stats.Count == 2 && definitions.Stats["Example"].Steps[1] == 5);
        readOnly = false;
        try { ((IDictionary<string, ShrineStatDefinition>)definitions.Stats).Clear(); }
        catch (NotSupportedException) { readOnly = true; }
        check(readOnly);
        var catalog = new DataCatalog();
        catalog.RegisterDefinition(definitions);
        check(ReferenceEquals(catalog.GetDefinition<ShrineDefinitions>(), definitions));
        catalog.Register(new DataTable<Language>(new[] { new Language(1, "문구", "") }, item => item.ID));
        check(catalog.GetTable<Language>().Get(1).KO == "문구" && ReferenceEquals(catalog.GetDefinition<ShrineDefinitions>(), definitions));
        var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        try
        {
            await new JsonLoader<ShrineLevelDefinition>(new Utf8CsvFileSource(), reader.ReadLevel).LoadAsync(path, cancelled.Token);
            check(false);
        }
        catch (OperationCanceledException) { check(true); }
        finally { cancelled.Dispose(); }
    }
}
