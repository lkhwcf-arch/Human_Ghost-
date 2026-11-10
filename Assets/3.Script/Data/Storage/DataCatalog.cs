using System;
using System.Collections.Generic;

namespace HumanGhost.Data.Storage
{
    // 초기화 도중에만 등록하며 게임 기능에는 조회 인터페이스로 전달합니다.
    internal sealed class DataCatalog : IReadOnlyDataCatalog
    {
        private readonly Dictionary<Type, object> _definitions = new Dictionary<Type, object>();

        internal void Register<T>(IReadOnlyDataTable<T> table)
            => RegisterDefinition<IReadOnlyDataTable<T>>(table);

        internal void RegisterDefinition<T>(T definition) where T : class
            => _definitions.Add(typeof(T), definition ?? throw new ArgumentNullException(nameof(definition)));

        public IReadOnlyDataTable<T> GetTable<T>() => GetDefinition<IReadOnlyDataTable<T>>();

        public T GetDefinition<T>() where T : class
        {
            if (_definitions.TryGetValue(typeof(T), out object definition)) return (T)definition;
            throw new KeyNotFoundException($"초기화된 데이터 정의를 찾을 수 없습니다. 데이터 형식: {typeof(T).Name}");
        }
    }
}
