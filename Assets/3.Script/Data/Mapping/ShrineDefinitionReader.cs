using System.Collections.Generic;
using System.Linq;
using HumanGhost.Data.Definitions;
using Newtonsoft.Json.Linq;

namespace HumanGhost.Data.Mapping
{
    // 배포 전에 검사된 JSON을 정의로 변환합니다. 파일 입력·구매·UI·검사기는 소유하지 않습니다.
    public class ShrineDefinitionReader
    {
        public ShrineLevelDefinition ReadLevel(string text)
        {
            var root = JObject.Parse(text);
            return new ShrineLevelDefinition(JsonValue.Read<int>(root, "maxLevel"), JsonValue.Read<int>(root, "soulCurrencyId"),
                JsonValue.Read<int>(root, "deathSoulLossRate"), JsonValue.Read<int>(root, "soulCostBase"), JsonValue.Read<int>(root, "soulCostRate"),
                ReadSteps(root, "levels", "level", "soulCostToNext"));
        }

        public ShrineStatDefinition ReadStat(string text)
        {
            var root = JObject.Parse(text);
            return new ShrineStatDefinition(JsonValue.Read<string>(root, "statType"), JsonValue.Read<int>(root, "textId"),
                JsonValue.Read<string>(root, "target"), JsonValue.Read<bool>(root, "enabled"), JsonValue.Read<int>(root, "maxStep"),
                ReadSteps(root, "steps", "step", "value"));
        }

        private static IEnumerable<KeyValuePair<int, int>> ReadSteps(JToken root, string rows, string key, string value)
            => root[rows].Select(row => new KeyValuePair<int, int>(JsonValue.Read<int>(row, key), JsonValue.Read<int>(row, value)));
    }
}
