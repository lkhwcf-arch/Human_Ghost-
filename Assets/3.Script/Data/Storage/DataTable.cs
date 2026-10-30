using System;
using System.Collections.Generic;

namespace HumanGhost.Data.Storage
{
    // 검사가 끝난 정의 객체를 초기화 시 보관하고 이후에는 조회만 제공합니다.
    public sealed class DataTable<T> : IReadOnlyDataTable<T>
    {
        private readonly Dictionary<int, T> _byId;
        private readonly IReadOnlyList<T> _definitions;

        public int Count => _byId.Count;
        public IReadOnlyList<T> All => _definitions;

        public DataTable(IEnumerable<T> definitions, Func<T, int> selectId)
        {
            if (definitions == null)
                throw new ArgumentNullException(nameof(definitions), "보관할 데이터 목록이 필요합니다.");
            if (selectId == null)
                throw new ArgumentNullException(nameof(selectId), "데이터 식별자 선택 함수가 필요합니다.");

            _byId = new Dictionary<int, T>();
            var copy = new List<T>();
            foreach (T definition in definitions)
            {
                _byId.Add(selectId(definition), definition);
                copy.Add(definition);
            }
            _definitions = copy.AsReadOnly();
        }

        public T Get(int id)
        {
            if (TryGet(id, out T definition)) return definition;
            throw new KeyNotFoundException($"요청한 데이터를 찾을 수 없습니다. 데이터 형식: {typeof(T).Name}, 식별자: {id}");
        }

        public bool TryGet(int id, out T definition)
            => _byId.TryGetValue(id, out definition);
    }
}
