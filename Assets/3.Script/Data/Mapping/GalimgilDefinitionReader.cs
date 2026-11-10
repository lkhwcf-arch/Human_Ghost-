using System.Linq;
using HumanGhost.Data.Definitions;
using Newtonsoft.Json.Linq;

namespace HumanGhost.Data.Mapping
{
    public class GalimgilDefinitionReader
    {
        public GalimgilRewardDefinition Read(string text)
        {
            var root = JObject.Parse(text);
            return new GalimgilRewardDefinition(JsonValue.Read<int>(root, "soulCurrencyId"), JsonValue.Read<int>(root, "baseSoul"),
                JsonValue.Read<int>(root, "soulPerKill"), JsonValue.Read<int>(root, "soulPerInteraction"), JsonValue.Read<bool>(root, "showGrade"),
                root["countStages"].Select(row => (int)JsonValue.Parse(row, typeof(int))),
                root["grades"].Select(row => new GalimgilGrade(JsonValue.Read<string>(row, "grade"), JsonValue.Read<int>(row, "minKillCount"))));
        }
    }
}
