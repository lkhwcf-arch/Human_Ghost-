using System.Collections.Generic;

namespace HumanGhost.Data.Storage
{
    public interface IReadOnlyDataTable<T>
    {
        int Count { get; }
        IReadOnlyList<T> All { get; }
        T Get(int id);
        bool TryGet(int id, out T definition);
    }
}
