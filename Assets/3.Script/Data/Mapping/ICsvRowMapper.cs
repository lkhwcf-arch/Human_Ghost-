using HumanGhost.Data.Csv;

namespace HumanGhost.Data.Mapping
{
    public interface ICsvRowMapper<T>
    {
        T Map(CsvRow row);
    }
}
