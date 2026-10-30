using System.Collections.Generic;

namespace HumanGhost.Data.Mapping
{
    public sealed class UnitStateGroup<T>
    {
        public int UnitID { get; }
        public IReadOnlyList<T> States { get; }

        public UnitStateGroup(int unitId, IEnumerable<T> states)
        {
            UnitID = unitId;
            States = new List<T>(states).AsReadOnly();
        }
    }
}
