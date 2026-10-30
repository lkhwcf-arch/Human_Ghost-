using System;
using System.Linq;
using System.Reflection;
using HumanGhost.Data.Definitions;

namespace HumanGhost.Data.Mapping
{
    // 생성자와 속성의 연결은 자료형별로 한 번만 계산하고 두 데이터 형식에서 함께 사용합니다.
    internal sealed class DefinitionMapper<T>
    {
        private static readonly ConstructorInfo _constructor = typeof(T).GetConstructors().Single();
        private static readonly ParameterInfo[] _parameters = _constructor.GetParameters();
        private static readonly string[] _columns = _parameters.Select(parameter => parameter.ParameterType == typeof(UnitStat)
            ? null : typeof(T).GetProperties().Single(property => string.Equals(property.Name, parameter.Name, StringComparison.OrdinalIgnoreCase)).Name).ToArray();
        private readonly Func<int, UnitStat> _resolveUnitStat;

        public DefinitionMapper(Func<int, UnitStat> resolveUnitStat = null)
        {
            if (_parameters.Any(parameter => parameter.ParameterType == typeof(UnitStat)) && resolveUnitStat == null)
                throw new ArgumentNullException(nameof(resolveUnitStat), "캐릭터 데이터 변환에는 공통 스탯 조회 함수가 필요합니다.");
            _resolveUnitStat = resolveUnitStat;
        }

        public T Map(Func<string, Type, object> readValue)
        {
            var arguments = new object[_parameters.Length];
            for (int index = 0; index < _parameters.Length; index++)
            {
                Type type = _parameters[index].ParameterType;
                arguments[index] = type == typeof(UnitStat)
                    ? _resolveUnitStat((int)readValue("UnitID", typeof(int)))
                    : readValue(_columns[index], type);
            }
            return (T)_constructor.Invoke(arguments);
        }
    }
}
