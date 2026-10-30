using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using HumanGhost.Data.Csv;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Mapping;

internal static class TableMappingChecks
{
    public static void Run(Action<bool> check)
    {
        var units = Read("UnitStat", new CsvDefinitionMapper<UnitStat>(), check).ToDictionary(unit => unit.ID);
        Read("Buff", new CsvDefinitionMapper<Buff>(), check);
        Read("Language", new CsvDefinitionMapper<Language>(), check);
        Read("Skill", new CsvDefinitionMapper<Skill>(), check);
        Read("StageLayer", new CsvDefinitionMapper<StageLayer>(), check);
        Read("State", new CsvDefinitionMapper<State>(), check);
        var requested = new List<int>();
        UnitStat ResolveUnitStat(int id) { requested.Add(id); return units[id]; }
        IReadOnlyList<Player> players = Read("Player", new CsvDefinitionMapper<Player>(ResolveUnitStat), check);
        IReadOnlyList<Monster> monsters = Read("Monster", new CsvDefinitionMapper<Monster>(ResolveUnitStat), check);
        check(requested.SequenceEqual(players.Select(player => player.UnitID).Concat(monsters.Select(monster => monster.UnitID))));
        foreach (Player player in players) CheckCommon(player, units[player.UnitID], check);
        foreach (Monster monster in monsters) CheckCommon(monster, units[monster.UnitID], check);

        // 컬럼 간 값이 뒤바뀌지 않는지와 음수·빈칸·플래그 변환을 확인합니다.
        CsvRow row = new CsvParser().Read("ID,Key,StatType,Value,Duration,TickInterval,UseTime,Charges,Stackable\n1,StatAdd,ATK,-3,11,22,33,44,Y").Rows[0];
        Buff buff = new CsvDefinitionMapper<Buff>().Map(row);
        check(buff.Value == -3 && buff.Duration == 11 && buff.TickInterval == 22 && buff.UseTime == 33 && buff.Charges == 44 && buff.Stackable);
        CsvDocument unitDocument = new CsvParser().Read("ID,TextID,Type,Tier,HP,ATK,DEF,Dodge,Crit,CritDamage,Poise,MoveSpeed,ArmorPen\n1,2,Player,,101,102,103,104,105,106,107,108,109");
        UnitStat common = new CsvDefinitionMapper<UnitStat>().Map(unitDocument.Rows[0]);
        check(common.Tier == null && common.HP == 101 && common.ATK == 102 && common.DEF == 103 && common.Dodge == 104 && common.Crit == 105 && common.CritDamage == 106 && common.Poise == 107 && common.MoveSpeed == 108 && common.ArmorPen == 109);
    }

    private static IReadOnlyList<T> Read<T>(string name, ICsvRowMapper<T> rowMapper, Action<bool> check)
    {
        CsvDocument document = new CsvParser().Read(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, name + ".csv")));
        IReadOnlyList<T> definitions = new CsvTableMapper<T>(rowMapper).Map(document);
        check(definitions.Count == document.Rows.Count);
        // 개발용 검사에서만 리플렉션으로 전체 컬럼과 프로퍼티 값을 비교합니다.
        for (int index = 0; index < definitions.Count; index++)
            foreach (string column in document.Headers)
            {
                object value = typeof(T).GetProperty(column).GetValue(definitions[index]);
                string text = value is bool flag ? (flag ? "Y" : "N") : Convert.ToString(value, CultureInfo.InvariantCulture);
                check(text == document.Rows[index].Get(column));
            }
        return definitions;
    }

    private static void CheckCommon(CharacterStat actual, UnitStat expected, Action<bool> check)
    {
        foreach (var property in typeof(CharacterStat).GetProperties())
            check(Equals(property.GetValue(actual), property.GetValue(expected)));
    }
}
