using System;
using HumanGhost.Data.Definitions;
using Newtonsoft.Json.Linq;

namespace HumanGhost.Data.Mapping
{
    public sealed class JsonDefinitionMapper<T>
    {
        private readonly DefinitionMapper<T> _mapper;

        public JsonDefinitionMapper(Func<int, UnitStat> resolveUnitStat = null)
        {
            _mapper = new DefinitionMapper<T>(resolveUnitStat);
        }

        public T Map(JObject row)
            => _mapper.Map((column, type) => JsonValue.Parse(row[column], type));
    }
}
