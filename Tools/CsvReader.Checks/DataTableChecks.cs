using System;
using System.Collections.Generic;
using System.IO;
using HumanGhost.Data.Csv;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Mapping;
using HumanGhost.Data.Storage;

internal static class DataTableChecks
{
    public static void Run(Action<bool> check)
    {
        var first = new Language(30, "첫 번째", "");
        var second = new Language(10, "두 번째", "");
        var source = new List<Language> { first, second };
        IReadOnlyDataTable<Language> table = new DataTable<Language>(source, definition => definition.ID);
        check(table.Count == 2 && ReferenceEquals(table.Get(10), second));
        check(table.TryGet(30, out Language found) && ReferenceEquals(found, first));
        check(!table.TryGet(999, out Language missing) && missing == null);
        source.Clear();
        check(table.Count == 2 && ReferenceEquals(table.All[0], first) && ReferenceEquals(table.All[1], second));
        bool failed = false;
        try { ((IList<Language>)table.All).Clear(); }
        catch (NotSupportedException) { failed = true; }
        check(failed && table.Count == 2);
        failed = false;
        try { table.Get(999); }
        catch (KeyNotFoundException error) { failed = error.Message.Contains("데이터를 찾을 수 없습니다.") && error.Message.Contains("999"); }
        check(failed);
        var empty = new DataTable<Language>(Array.Empty<Language>(), definition => definition.ID);
        check(empty.Count == 0 && empty.All.Count == 0 && !empty.TryGet(30, out _));

        CsvDocument ReadCsv(string name) => new CsvParser().Read(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, name + ".csv")));
        var unitDefinitions = new CsvTableMapper<UnitStat>(new CsvDefinitionMapper<UnitStat>()).Map(ReadCsv("UnitStat"));
        IReadOnlyDataTable<UnitStat> units = new DataTable<UnitStat>(unitDefinitions, definition => definition.ID);
        var players = new CsvTableMapper<Player>(new CsvDefinitionMapper<Player>(units.Get)).Map(ReadCsv("Player"));
        IReadOnlyDataTable<Player> playerTable = new DataTable<Player>(players, definition => definition.ID);
        foreach (Player player in players)
        {
            check(ReferenceEquals(playerTable.Get(player.ID), player));
            foreach (var property in typeof(CharacterStat).GetProperties())
                check(Equals(property.GetValue(player), property.GetValue(units.Get(player.UnitID))));
        }

        string json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "PlayerState.json"));
        var groups = new JsonTableReader().ReadGroups<PlayerState>(json);
        IReadOnlyDataTable<UnitStateGroup<PlayerState>> states = new DataTable<UnitStateGroup<PlayerState>>(groups, group => group.UnitID);
        check(states.Count == groups.Count);
        foreach (var group in groups) check(ReferenceEquals(states.Get(group.UnitID), group));
    }
}
