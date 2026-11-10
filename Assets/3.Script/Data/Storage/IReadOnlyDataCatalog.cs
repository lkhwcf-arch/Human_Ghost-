namespace HumanGhost.Data.Storage
{
    public interface IReadOnlyDataCatalog
    {
        IReadOnlyDataTable<T> GetTable<T>();
        T GetDefinition<T>() where T : class;
    }
}
