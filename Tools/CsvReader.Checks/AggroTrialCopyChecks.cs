using System;
using System.IO;
using System.Linq;
using HumanGhost.Data.Csv;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Mapping;

internal static class AggroTrialCopyChecks
{
    public static void Run(Action<bool> check)
    {
        var document = new CsvParser().Read(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "MonsterAggro.csv")));
        var sourceMapper = new CsvDefinitionMapper<MonsterAggro>();
        var copyMapper = new DefinitionMapper<MonsterAggro>();
        var properties = typeof(MonsterAggro).GetProperties();
        foreach (var row in document.Rows)
        {
            MonsterAggro source = sourceMapper.Map(row);
            var original = properties.ToDictionary(property => property.Name, property => property.GetValue(source));
            check(properties.All(property => property.PropertyType == typeof(int)));
            MonsterAggro copy = copyMapper.Map((column, type) => original[column]);
            check(!ReferenceEquals(source, copy));
            foreach (var property in properties) check(Equals(property.GetValue(copy), original[property.Name]));
            // 시험 사본 하나의 값만 바꾸고 실제 배포 원본과 다른 속성들이 보존되는지 확인합니다.
            int changed = source.FrontRange == 0 ? 1 : 0;
            MonsterAggro edited = copyMapper.Map((column, type) => column == nameof(MonsterAggro.FrontRange) ? changed : original[column]);
            check(edited.FrontRange == changed && edited.ID == source.ID && edited.MonsterID == source.MonsterID);
            foreach (var property in properties)
            {
                check(Equals(property.GetValue(source), original[property.Name]));
                if (property.Name != nameof(MonsterAggro.FrontRange))
                    check(Equals(property.GetValue(edited), original[property.Name]));
            }
        }
    }
}
