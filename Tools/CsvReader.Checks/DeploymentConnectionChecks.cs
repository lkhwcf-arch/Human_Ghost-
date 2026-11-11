using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using HumanGhost.Data.Csv;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Storage;
using HumanGhost.Gameplay;
using Newtonsoft.Json.Linq;

// 배포된 자료를 실제 초기화 경로로 읽은 뒤 참조·정의·런타임 연결을 검사합니다.
internal static class DeploymentConnectionChecks
{
    private static readonly Dictionary<string, string> _references = new Dictionary<string, string>
    {
        ["UnitID"] = "UnitStat", ["StageID"] = "Stage", ["NextStage"] = "Stage",
        ["MonsterID"] = "Monster", ["PlayerID"] = "Player", ["StateID"] = "State",
        ["EffectID"] = "StatusEffect", ["CurrencyID"] = "Currency", ["CutsceneID"] = "Cutscene",
        ["SkillID"] = "Skill", ["WeaponID"] = "Item", ["StartWeapon"] = "Item"
    };

    public static void Run(IReadOnlyDataCatalog data, Action<bool> check)
    {
        void Require(bool valid, string location)
        {
            if (!valid) throw new InvalidOperationException("배포 데이터 연결 오류: " + location);
            check(valid);
        }
        var tables = new Dictionary<string, Dictionary<int, object>>(StringComparer.Ordinal);
        Dictionary<int, object> Table(string name)
        {
            if (tables.TryGetValue(name, out var existing)) return existing;
            Type type = typeof(Stage).Assembly.GetType("HumanGhost.Data.Definitions." + name);
            Require(type != null, name + "의 코드 정의가 없습니다.");
            object table = typeof(IReadOnlyDataCatalog).GetMethod("GetTable").MakeGenericMethod(type).Invoke(data, null);
            var all = (IEnumerable)typeof(IReadOnlyDataTable<>).MakeGenericType(type).GetProperty("All").GetValue(table);
            var rows = all.Cast<object>().ToDictionary(row => (int)type.GetProperty("ID").GetValue(row));
            tables.Add(name, rows);
            return rows;
        }
        void Reference(string column, object value, string location)
        {
            if (value == null || !(value is int id)) return;
            string target = column.EndsWith("TextID", StringComparison.Ordinal) ? "Language"
                : _references.TryGetValue(column, out string table) ? table : null;
            // 없음·미사용을 뜻하는 0은 기존 계약을 유지합니다. 필수값의 상세 규칙은 원본 검사기가 소유합니다.
            if (target == null || id == 0) return;
            Require(id > 0 && Table(target).ContainsKey(id), location + " → " + target + ".ID=" + id + " 참조가 없습니다.");
        }
        foreach (string path in Directory.GetFiles(AppContext.BaseDirectory, "*.csv").OrderBy(path => path))
        {
            string name = Path.GetFileNameWithoutExtension(path);
            var rows = Table(name);
            CsvDocument source = new CsvParser().Read(File.ReadAllText(path));
            Require(rows.Count == source.Rows.Count, name + "의 원본·등록 행 수가 다릅니다.");
            foreach (CsvRow row in source.Rows)
            {
                int id = int.Parse(row.Get("ID"), CultureInfo.InvariantCulture);
                Require(id > 0 && rows.ContainsKey(id), name + ".ID=" + id + "가 등록되지 않았습니다.");
                object actual = rows[id];
                foreach (string column in source.Headers)
                {
                    var property = actual.GetType().GetProperty(column);
                    Require(property != null, name + "." + column + "의 코드 속성이 없습니다.");
                    object value = property.GetValue(actual);
                    string text = value is bool flag ? flag ? "Y" : "N" : Convert.ToString(value, CultureInfo.InvariantCulture);
                    string location = name + "[" + id + "]." + column;
                    Require(text == row.Get(column), location + "이 원본과 다릅니다.");
                    if (value is int number)
                    {
                        // 효과 증감과 앞뒤 정렬 순서는 부호 자체가 의미이므로 양수로 강제하지 않습니다.
                        if (column != "Value" && column != "SortOrder") Require(number >= 0, location + "에 음수가 있습니다.");
                        if (column == "HP") Require(number > 0, location + "는 양수여야 합니다.");
                        if (column == "Dodge" || column == "Crit" || column == "DeathLossRate")
                            Require(number <= 10000, location + "이 확률 분모 10000을 넘습니다.");
                    }
                    Reference(column, value, location);
                }
                var condition = actual.GetType().GetProperty("UnlockConditionType");
                if (condition != null && (int)condition.GetValue(actual) == 1)
                    Reference("StageID", actual.GetType().GetProperty("UnlockTargetID").GetValue(actual), name + "[" + id + "].UnlockTargetID");
            }
        }
        // 구조화 JSON 안의 표 참조도 같은 연결 규칙을 사용합니다.
        foreach (string name in new[] { "Item", "PlayerState", "BossState", "EliteState", "MonsterState" })
        {
            string path = Path.Combine(AppContext.BaseDirectory, name + ".json");
            if (!File.Exists(path)) continue;
            var source = (JContainer)JToken.Parse(File.ReadAllText(path));
            foreach (JObject row in source.DescendantsAndSelf().OfType<JObject>())
                foreach (JProperty property in row.Properties())
                    if (property.Value.Type == JTokenType.Integer)
                        Reference(property.Name, (int)property.Value, name + ".json:" + property.Path);
        }
        var attack = new AttackSpec(1, 1, 0, 0, 0, 0, 0);
        foreach (Player definition in data.GetTable<Player>().All)
        {
            var actor = new PlayerActor(definition, Enumerable.Repeat(attack, 5).ToArray(), true, definition.StaminaMax);
            Require(actor.Health == definition.HP && actor.MaxHealth == definition.HP, "Player[" + definition.ID + "]의 CSV HP가 런타임에 전달되지 않았습니다.");
            Require(actor.Stamina.Current == definition.StaminaMax, "Player[" + definition.ID + "]의 스태미나 연결 오류");
            actor.SetMove(1);
            Require(actor.Locomotion.HorizontalSpeed == definition.MoveSpeed, "Player[" + definition.ID + "]의 이동값 연결 오류");
        }
        foreach (Monster definition in data.GetTable<Monster>().All.Where(row => row.ContentEnabled))
        {
            var pattern = new MonsterPattern(StateKey.Attack1, definition.AttackRange, attack);
            var actor = new MonsterActor(definition, new[] { pattern }, 0, 0, (min, max) => min);
            Require(actor.Health == definition.HP, "Monster[" + definition.ID + "]의 CSV HP가 런타임에 전달되지 않았습니다.");
            int before = actor.Health;
            actor.ReceiveHit(1);
            Require(actor.Health == Math.Max(0, before - 1) && definition.HP == before, "Monster[" + definition.ID + "]의 피해 처리·원본 보존 연결 오류");
        }
        foreach (Boss definition in data.GetTable<Boss>().All)
        {
            Require(definition.States.Count > 0, "Boss[" + definition.ID + "]의 상태 정의가 없습니다.");
            var actor = new BossPoise(definition, definition.States[0]);
            Require(actor.Health == definition.HP, "Boss[" + definition.ID + "]의 CSV HP가 런타임에 전달되지 않았습니다.");
        }
        var galimgil = data.GetDefinition<GalimgilRewardDefinition>();
        Reference("CurrencyID", galimgil.SoulCurrencyId, "galimgil_reward.soulCurrencyId");
        foreach (int stageId in galimgil.CountStages) Reference("StageID", stageId, "galimgil_reward.countStages");
        var shrine = data.GetDefinition<ShrineDefinitions>();
        Reference("CurrencyID", shrine.Level.SoulCurrencyId, "shrine_level.soulCurrencyId");
        foreach (var stat in shrine.Stats.Values) Reference("TextID", stat.TextId, "shrine:" + stat.StatType + ".textId");
    }
}
