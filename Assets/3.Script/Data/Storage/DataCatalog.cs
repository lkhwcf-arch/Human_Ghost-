using System;
using System.Collections.Generic;

namespace HumanGhost.Data.Storage
{
    // 초기화 도중에만 등록하며 게임 기능에는 조회 인터페이스로 전달합니다.
    internal sealed class DataCatalog : IReadOnlyDataCatalog
    {
        private readonly Dictionary<Type, object> _tables = new Dictionary<Type, object>();

        internal void Register<T>(IReadOnlyDataTable<T> table)
            => _tables.Add(typeof(T), table);

        public IReadOnlyDataTable<T> GetTable<T>()
        {
            if (_tables.TryGetValue(typeof(T), out object table)) return (IReadOnlyDataTable<T>)table;
            throw new KeyNotFoundException($"초기화된 데이터 테이블을 찾을 수 없습니다. 데이터 형식: {typeof(T).Name}");
        }
    }
}
