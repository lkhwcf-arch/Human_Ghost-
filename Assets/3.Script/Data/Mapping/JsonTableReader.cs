using System;
using System.Collections.Generic;
using HumanGhost.Data.Definitions;
using Newtonsoft.Json.Linq;

namespace HumanGhost.Data.Mapping
{
    public sealed class JsonTableReader
    {
        public IReadOnlyList<T> Read<T>(string text)
            => ReadRows<T>(ParseArray(text));

        public IReadOnlyList<UnitStateGroup<T>> ReadGroups<T>(string text)
        {
            var groups = new List<UnitStateGroup<T>>();
            foreach (JObject group in ParseArray(text))
                groups.Add(new UnitStateGroup<T>(group.Value<int>("UnitID"), ReadRows<T>((JArray)group["States"])));
            return groups.AsReadOnly();
        }

        public IReadOnlyList<TCharacter> ReadCharacters<TCharacter, TState>(string text,
            Func<int, UnitStat> resolveUnitStat, Func<UnitStat, IReadOnlyList<TState>, TCharacter> compose)
        {
            var characters = new List<TCharacter>();
            foreach (UnitStateGroup<TState> group in ReadGroups<TState>(text))
                characters.Add(compose(resolveUnitStat(group.UnitID), group.States));
            return characters.AsReadOnly();
        }

        private static IReadOnlyList<T> ReadRows<T>(JArray rows)
        {
            var mapper = new JsonDefinitionMapper<T>();
            var definitions = new List<T>(rows.Count);
            foreach (JObject row in rows) definitions.Add(mapper.Map(row));
            return definitions.AsReadOnly();
        }

        private static JArray ParseArray(string text)
            => JArray.Parse(text.Length > 0 && text[0] == '\uFEFF' ? text.Substring(1) : text);
    }
}
