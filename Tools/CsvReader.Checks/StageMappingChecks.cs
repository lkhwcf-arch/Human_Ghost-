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
            var document = parser.Read("UnlockRequiredValue,UnlockTargetID,UnlockConditionType,ContentEnabled,Active,SpawnFile,BGM_Battle,BGM_Normal,NextStage,Order,TextID,ID\n3,8005,2,N,N,,\"battle,music\",,,5,30014,8004\n");
            IReadOnlyList<Stage> definitions = mapper.Map(document);
            Stage stage = definitions[0];
            check(stage.ID == 8004 && stage.TextID == 30014 && stage.Order == 5);
            check(stage.NextStage == null && stage.BGM_Normal == "" && stage.SpawnFile == "");
            check(stage.BGM_Battle == "battle,music" && !stage.Active);
            // 잠금 정수 3칸이 서로 뒤바뀌지 않고 사용 여부 플래그를 그대로 읽는지 확인합니다.
            check(!stage.ContentEnabled && stage.UnlockConditionType == 2 && stage.UnlockTargetID == 8005 && stage.UnlockRequiredValue == 3);
            check(((ICollection<Stage>)definitions).IsReadOnly);
            check(mapper.Map(parser.Read("ID,TextID,Order,NextStage,BGM_Normal,BGM_Battle,SpawnFile,Active,ContentEnabled,UnlockConditionType,UnlockTargetID,UnlockRequiredValue\n")).Count == 0);
            // 새 컬럼이 빠진 이전 형식은 기본값으로 숨기지 않고 실패해야 합니다.
            bool failed = false;
            try { mapper.Map(parser.Read("ID,TextID,Order,NextStage,BGM_Normal,BGM_Battle,SpawnFile,Active\n8001,30011,1,8002,,,,Y\n")); }
            catch (KeyNotFoundException) { failed = true; }
            check(failed);
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
                && stage.SpawnFile == row.Get("SpawnFile") && stage.Active == (row.Get("Active") == "Y")
                && stage.ContentEnabled == (row.Get("ContentEnabled") == "Y")
                && stage.UnlockConditionType.ToString(CultureInfo.InvariantCulture) == row.Get("UnlockConditionType")
                && stage.UnlockTargetID.ToString(CultureInfo.InvariantCulture) == row.Get("UnlockTargetID")
                && stage.UnlockRequiredValue.ToString(CultureInfo.InvariantCulture) == row.Get("UnlockRequiredValue"));
        }
    }
}
