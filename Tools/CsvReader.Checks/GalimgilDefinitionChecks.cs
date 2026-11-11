using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Mapping;

internal static class GalimgilDefinitionChecks
{
    public static void Run(Action<bool> check)
    {
        var value = new GalimgilDefinitionReader().Read(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "galimgil_reward.json")));
        check(value.SoulCurrencyId == 1 && value.BaseSoul == 0 && value.SoulPerKill == 3 && value.SoulPerInteraction == 3);
        check(value.CountStages.SequenceEqual(new[] { 8002 }) && !value.ShowGrade);
        check(value.Grades.Select(row => row.MinKillCount).SequenceEqual(new[] { 8, 5, 0 }));
        var stages = new List<int> { 8002 };
        var grades = new List<GalimgilGrade> { new GalimgilGrade("LOW", 0) };
        var copy = new GalimgilRewardDefinition(1, 0, 3, 3, false, stages, grades);
        stages.Clear(); grades.Clear();
        check(copy.CountStages.Single() == 8002 && copy.Grades.Single().Key == "LOW");
        bool readOnly = false;
        try { ((IList<int>)copy.CountStages)[0] = 8003; } catch (NotSupportedException) { readOnly = true; }
        check(readOnly);
    }
}
