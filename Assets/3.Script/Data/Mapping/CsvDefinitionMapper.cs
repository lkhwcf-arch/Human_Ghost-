using System;
using HumanGhost.Data.Csv;
using HumanGhost.Data.Definitions;

namespace HumanGhost.Data.Mapping
{
    // 타입마다 생성자 정보를 한 번만 읽고, 로딩 단계에서만 객체를 구성합니다.
    public sealed class CsvDefinitionMapper<T> : ICsvRowMapper<T>
    {
        private readonly DefinitionMapper<T> _mapper;

        public CsvDefinitionMapper(Func<int, UnitStat> resolveUnitStat = null)
        {
            _mapper = new DefinitionMapper<T>(resolveUnitStat);
        }

        public T Map(CsvRow row)
        {
            return _mapper.Map((column, type) => CsvValue.Parse(row.Get(column), type));
        }

    }
}
