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
            return new ShrineLevelDefinition(Read<int>(root, "maxLevel"), Read<int>(root, "soulCurrencyId"),
                Read<int>(root, "deathSoulLossRate"), Read<int>(root, "soulCostBase"), Read<int>(root, "soulCostRate"),
                ReadSteps(root, "levels", "level", "soulCostToNext"));
        }

        public ShrineStatDefinition ReadStat(string text)
        {
            var root = JObject.Parse(text);
            return new ShrineStatDefinition(Read<string>(root, "statType"), Read<int>(root, "textId"),
                Read<string>(root, "target"), Read<bool>(root, "enabled"), Read<int>(root, "maxStep"),
                ReadSteps(root, "steps", "step", "value"));
        }

        private static T Read<T>(JToken row, string name) => (T)JsonValue.Parse(row[name], typeof(T));
        private static IEnumerable<KeyValuePair<int, int>> ReadSteps(JToken root, string rows, string key, string value)
            => root[rows].Select(row => new KeyValuePair<int, int>(Read<int>(row, key), Read<int>(row, value)));
    }
}
