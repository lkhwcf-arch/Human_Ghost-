using System.Collections.Generic;
using HumanGhost.Data.Csv;

namespace HumanGhost.Data.Mapping
{
    // 행 변환만 조립하며 파일 입력과 데이터 저장소를 소유하지 않습니다.
    public sealed class CsvTableMapper<T>
    {
        private readonly ICsvRowMapper<T> _rowMapper;

        public CsvTableMapper(ICsvRowMapper<T> rowMapper)
        {
            _rowMapper = rowMapper;
        }

        public IReadOnlyList<T> Map(CsvDocument document)
        {
            var definitions = new List<T>(document.Rows.Count);
            foreach (CsvRow row in document.Rows)
                definitions.Add(_rowMapper.Map(row));
            return definitions.AsReadOnly();
        }
    }
}
