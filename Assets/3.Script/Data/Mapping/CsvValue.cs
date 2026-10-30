using System;
using System.Globalization;
using HumanGhost.Data.Csv;

namespace HumanGhost.Data.Mapping
{
    // 배포 전에 검사한 값을 데이터 정의에서 사용하는 타입으로 변환합니다.
    public static class CsvValue
    {
        public static int Integer(CsvRow row, string column)
            => (int)Parse(row.Get(column), typeof(int));

        public static int? OptionalInteger(CsvRow row, string column)
        {
            return (int?)Parse(row.Get(column), typeof(int?));
        }

        public static bool Flag(CsvRow row, string column)
            => (bool)Parse(row.Get(column), typeof(bool));

        public static object Parse(string value, Type type)
        {
            Type underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null && value.Length == 0) return null;
            type = underlying ?? type;
            if (type == typeof(string)) return value;
            if (type == typeof(int)) return int.Parse(value, CultureInfo.InvariantCulture);
            if (type == typeof(bool)) return value == "Y";
            throw new NotSupportedException($"CSV 변환에서 지원하지 않는 타입입니다: {type.Name}");
        }
    }
}
