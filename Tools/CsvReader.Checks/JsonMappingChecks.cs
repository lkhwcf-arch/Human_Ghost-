using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HumanGhost.Data.Csv;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Infrastructure;
using HumanGhost.Data.Json;
using HumanGhost.Data.Mapping;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

internal static class JsonMappingChecks
{
    public static async Task RunAsync(Action<bool> check)
    {
        var reader = new JsonTableReader();
        string ReadText(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, name + ".json"));
        string itemText = ReadText("Item");
        IReadOnlyList<Item> items = reader.Read<Item>(itemText);
        CompareRows(JArray.Parse(itemText), items, check);
        check(((ICollection<Item>)items).IsReadOnly);
        CompareGroups(ReadText("PlayerState"), reader.ReadGroups<PlayerState>, check);
        CompareGroups(ReadText("BossState"), reader.ReadGroups<BossState>, check);
        CompareGroups(ReadText("EliteState"), reader.ReadGroups<EliteState>, check);
        check(reader.Read<Item>("[]").Count == 0 && reader.ReadGroups<EliteState>("[]").Count == 0);
        check(reader.Read<Item>("\uFEFF" + itemText).Count == items.Count);

        CsvDocument document = new CsvParser().Read(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "UnitStat.csv")));
        var units = new CsvTableMapper<UnitStat>(new CsvDefinitionMapper<UnitStat>()).Map(document).ToDictionary(unit => unit.ID);
        var requested = new List<int>();
        UnitStat Resolve(int id) { requested.Add(id); return units[id]; }
        IReadOnlyList<Boss> bosses = reader.ReadCharacters<Boss, BossState>(ReadText("BossState"), Resolve, (common, states) => new Boss(common, states));
        foreach (Boss boss in bosses)
        {
            check(boss.States.All(state => state.UnitID == boss.ID));
            foreach (var property in typeof(CharacterStat).GetProperties())
                check(Equals(property.GetValue(boss), property.GetValue(units[boss.ID])));
        }
        check(requested.SequenceEqual(bosses.Select(boss => boss.ID)));
        check(reader.ReadCharacters<Elite, EliteState>(ReadText("EliteState"), Resolve, (common, states) => new Elite(common, states)).Count == 0);
        var sampleGroup = new JArray(new JObject(new JProperty("UnitID", units.Keys.First()), new JProperty("States", new JArray())));
        var elites = reader.ReadCharacters<Elite, EliteState>(sampleGroup.ToString(), Resolve, (common, states) => new Elite(common, states));
        check(elites.Count == 1 && elites[0].States.Count == 0 && elites[0].ID == units.Keys.First());

        var optional = (JObject)JArray.Parse(itemText)[0].DeepClone();
        optional["TextID"] = null; optional["SkillID"] = null; optional["StatJson"] = "한글 경로"; optional["InGstar"] = false;
        Item mapped = new JsonDefinitionMapper<Item>().Map(optional);
        check(mapped.TextID == null && mapped.SkillID == null && mapped.StatJson == "한글 경로" && !mapped.InGstar);
        check((int)JsonValue.Parse(new JValue(int.MinValue), typeof(int)) == int.MinValue);
        bool failed = false;
        try { JsonValue.Parse(new JValue(1.5), typeof(int)); } catch (FormatException) { failed = true; }
        check(failed);
        failed = false;
        try { reader.Read<Item>("[{"); } catch (JsonReaderException) { failed = true; }
        check(failed);

        var loader = new JsonLoader<IReadOnlyList<Item>>(new Utf8CsvFileSource(), reader.Read<Item>);
        check((await loader.LoadAsync(Path.Combine(AppContext.BaseDirectory, "Item.json"), CancellationToken.None)).Count == items.Count);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        failed = false;
        try { await loader.LoadAsync("not-read.json", cancellation.Token); } catch (OperationCanceledException) { failed = true; }
        check(failed);
    }

    private static void CompareGroups<T>(string text, Func<string, IReadOnlyList<UnitStateGroup<T>>> read, Action<bool> check)
    {
        JArray expected = JArray.Parse(text);
        IReadOnlyList<UnitStateGroup<T>> groups = read(text);
        check(groups.Count == expected.Count && ((ICollection<UnitStateGroup<T>>)groups).IsReadOnly);
        for (int index = 0; index < groups.Count; index++)
        {
            check(groups[index].UnitID == expected[index].Value<int>("UnitID"));
            check(((ICollection<T>)groups[index].States).IsReadOnly);
            CompareRows((JArray)expected[index]["States"], groups[index].States, check);
        }
    }

    private static void CompareRows<T>(JArray expected, IReadOnlyList<T> actual, Action<bool> check)
    {
        check(expected.Count == actual.Count);
        for (int index = 0; index < actual.Count; index++)
            foreach (JProperty property in ((JObject)expected[index]).Properties())
            {
                object value = typeof(T).GetProperty(property.Name).GetValue(actual[index]);
                check(JToken.DeepEquals(value == null ? JValue.CreateNull() : JToken.FromObject(value), property.Value));
            }
    }
}
