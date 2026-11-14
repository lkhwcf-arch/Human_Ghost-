using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using HumanGhost.Data.Csv;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Mapping;

internal static class StageMappingChecks
{
    public static void Run(Action<bool> check)
    {
        var parser = new CsvParser();
        var mapper = new CsvTableMapper<Stage>(new CsvDefinitionMapper<Stage>());
        CsvDocument actual = parser.Read(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Stage.csv")));
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            string[] reversed = actual.Headers.Reverse().ToArray();
            var reordered = new CsvDocument(actual.SourceName, reversed, actual.Rows.Select(row =>
                new CsvRow(row.SourceLine, reversed, reversed.Select(row.Get).ToArray())));
            IReadOnlyList<Stage> stages = mapper.Map(reordered);
            check(stages.Count == actual.Rows.Count && ((ICollection<Stage>)stages).IsReadOnly);
            for (int index = 0; index < stages.Count; index++)
            {
                CsvRow row = actual.Rows[index];
                Stage stage = stages[index];
                check(stage.ID.ToString(CultureInfo.InvariantCulture) == row.Get("ID")
                    && stage.TextID.ToString(CultureInfo.InvariantCulture) == row.Get("TextID")
                    && stage.Order.ToString(CultureInfo.InvariantCulture) == row.Get("Order")
                    && (stage.NextStage?.ToString(CultureInfo.InvariantCulture) ?? "") == row.Get("NextStage")
                    && stage.BGM_Normal == row.Get("BGM_Normal") && stage.BGM_Battle == row.Get("BGM_Battle")
                    && stage.SpawnFile == row.Get("SpawnFile") && stage.Active == (row.Get("Active") == "Y")
                    && stage.ContentEnabled == (row.Get("ContentEnabled") == "Y")
                    && stage.UnlockConditionType.ToString(CultureInfo.InvariantCulture) == row.Get("UnlockConditionType")
                    && stage.UnlockTargetID.ToString(CultureInfo.InvariantCulture) == row.Get("UnlockTargetID")
                    && stage.UnlockRequiredValue.ToString(CultureInfo.InvariantCulture) == row.Get("UnlockRequiredValue"));
            }
            check(mapper.Map(new CsvDocument(actual.SourceName, actual.Headers, Array.Empty<CsvRow>())).Count == 0);
            if (actual.Rows.Count > 0)
            {
                string[] missing = actual.Headers.Where(name => name != "NextStage").ToArray();
                var oldFormat = new CsvDocument(actual.SourceName, missing, actual.Rows.Select(row =>
                    new CsvRow(row.SourceLine, missing, missing.Select(row.Get).ToArray())));
                bool failed = false;
                try { mapper.Map(oldFormat); } catch (KeyNotFoundException) { failed = true; }
                check(failed); // 누락 컬럼은 기본값으로 숨기지 않고 실패로 처리합니다.
            }
        }
        finally { CultureInfo.CurrentCulture = culture; }
    }
}
