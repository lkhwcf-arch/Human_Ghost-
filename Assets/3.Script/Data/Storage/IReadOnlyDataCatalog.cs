namespace HumanGhost.Data.Storage
{
    public interface IReadOnlyDataCatalog
    {
        IReadOnlyDataTable<T> GetTable<T>();
    }
}
