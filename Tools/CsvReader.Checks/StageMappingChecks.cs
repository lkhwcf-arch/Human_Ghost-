using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using HumanGhost.Data.Csv;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Mapping;

internal static class StageMappingChecks
{
    public static void Run(Action<bool> check)
    {
        var parser = new CsvParser();
        var mapper = new CsvTableMapper<Stage>(new CsvDefinitionMapper<Stage>());
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            // 열 순서와 현재 언어 설정에 영향받지 않고 빈 문자열과 값 없음을 구분합니다.
            var document = parser.Read("Active,SpawnFile,BGM_Battle,BGM_Normal,NextStage,Order,TextID,ID\nN,,\"battle,music\",,,5,30014,8004\n");
            IReadOnlyList<Stage> definitions = mapper.Map(document);
            Stage stage = definitions[0];
            check(stage.ID == 8004 && stage.TextID == 30014 && stage.Order == 5);
            check(stage.NextStage == null && stage.BGM_Normal == "" && stage.SpawnFile == "");
            check(stage.BGM_Battle == "battle,music" && !stage.Active);
            check(((ICollection<Stage>)definitions).IsReadOnly);
            check(mapper.Map(parser.Read("ID,TextID,Order,NextStage,BGM_Normal,BGM_Battle,SpawnFile,Active\n")).Count == 0);
        }
        finally { CultureInfo.CurrentCulture = culture; }

        CsvDocument actual = parser.Read(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Stage.csv")));
        IReadOnlyList<Stage> stages = mapper.Map(actual);
        check(stages.Count == actual.Rows.Count);
        for (int index = 0; index < stages.Count; index++)
        {
            CsvRow row = actual.Rows[index];
            Stage stage = stages[index];
            check(stage.ID.ToString(CultureInfo.InvariantCulture) == row.Get("ID")
                && stage.TextID.ToString(CultureInfo.InvariantCulture) == row.Get("TextID")
                && stage.Order.ToString(CultureInfo.InvariantCulture) == row.Get("Order")
                && (stage.NextStage?.ToString(CultureInfo.InvariantCulture) ?? "") == row.Get("NextStage")
                && stage.BGM_Normal == row.Get("BGM_Normal") && stage.BGM_Battle == row.Get("BGM_Battle")
                && stage.SpawnFile == row.Get("SpawnFile") && stage.Active == (row.Get("Active") == "Y"));
        }
    }
}
