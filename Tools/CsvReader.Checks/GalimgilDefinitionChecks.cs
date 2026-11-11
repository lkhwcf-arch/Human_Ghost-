using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Mapping;
using Newtonsoft.Json.Linq;

internal static class GalimgilDefinitionChecks
{
    public static void Run(Action<bool> check)
    {
        CheckMapping(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "galimgil_reward.json")), check);
        // 배포 데이터의 특정 수치에 의존하지 않도록 서로 다른 설정도 같은 검사로 읽습니다.
        CheckMapping(@"{
            ""soulCurrencyId"": 2, ""baseSoul"": 7, ""soulPerKill"": 5, ""soulPerInteraction"": 11,
            ""showGrade"": true, ""countStages"": [8003, 8001],
            ""grades"": [{""grade"": ""EXAMPLE_HIGH"", ""minKillCount"": 9}, {""grade"": ""EXAMPLE_LOW"", ""minKillCount"": 0}]
        }", check);
        var stages = new List<int> { 8002 };
        var grades = new List<GalimgilGrade> { new GalimgilGrade("LOW", 0) };
        var copy = new GalimgilRewardDefinition(1, 0, 3, 3, false, stages, grades);
        stages.Clear(); grades.Clear();
        check(copy.CountStages.Single() == 8002 && copy.Grades.Single().Key == "LOW");
        bool readOnly = false;
        try { ((IList<int>)copy.CountStages)[0] = 8003; } catch (NotSupportedException) { readOnly = true; }
        check(readOnly);
    }

    private static void CheckMapping(string source, Action<bool> check)
    {
        var expected = JObject.Parse(source);
        var actual = new GalimgilDefinitionReader().Read(source);
        void Match(bool matches, string field)
        {
            if (!matches) throw new InvalidOperationException("갈림길 정의를 읽은 결과가 원본과 다릅니다. 항목: " + field);
            check(matches);
        }
        Match(actual.SoulCurrencyId == (int)expected["soulCurrencyId"], "soulCurrencyId");
        Match(actual.BaseSoul == (int)expected["baseSoul"], "baseSoul");
        Match(actual.SoulPerKill == (int)expected["soulPerKill"], "soulPerKill");
        Match(actual.SoulPerInteraction == (int)expected["soulPerInteraction"], "soulPerInteraction");
        Match(actual.ShowGrade == (bool)expected["showGrade"], "showGrade");
        Match(actual.CountStages.SequenceEqual(expected["countStages"].Values<int>()), "countStages");
        Match(actual.Grades.Select(row => row.Key).SequenceEqual(expected["grades"].Select(row => (string)row["grade"])), "grades.grade");
        Match(actual.Grades.Select(row => row.MinKillCount).SequenceEqual(expected["grades"].Select(row => (int)row["minKillCount"])), "grades.minKillCount");
    }
}
