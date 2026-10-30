using System;
using Newtonsoft.Json.Linq;

namespace HumanGhost.Data.Mapping
{
    public static class JsonValue
    {
        public static object Parse(JToken value, Type type)
        {
            Type underlying = Nullable.GetUnderlyingType(type);
            if (value.Type == JTokenType.Null && (underlying != null || type == typeof(string))) return null;
            type = underlying ?? type;
            if (type == typeof(string)) return value.Value<string>();
            if (type == typeof(int)) return value.Type == JTokenType.Integer
                ? (object)value.Value<int>() : throw new FormatException("JSON 정수 필드에 정수 토큰이 필요합니다.");
            if (type == typeof(bool)) return value.Value<bool>();
            throw new NotSupportedException($"JSON 변환에서 지원하지 않는 타입입니다: {type.Name}");
        }
    }
}
