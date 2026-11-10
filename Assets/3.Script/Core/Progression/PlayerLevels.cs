using System;
using System.Collections.Generic;
using System.Linq;
using HumanGhost.Data.Definitions;

namespace HumanGhost.Gameplay
{
    // 경험치는 누적 보존하고, 표의 단계별 요구량으로 현재 레벨을 계산합니다.
    public class PlayerLevels
    {
        private readonly PlayerLevel[] _levels;
        private readonly long _experienceLimit;
        public PlayerLevels(IEnumerable<PlayerLevel> levels)
        {
            if (levels == null) throw new ArgumentNullException(nameof(levels));
            _levels = levels.OrderBy(row => row.Level).ToArray();
            if (_levels.Length == 0 || _levels[0].Level != 1 || !_levels[0].ContentEnabled)
                throw new ArgumentException("시작 레벨의 정의가 필요합니다.", nameof(levels));
            for (int index = 0; index + 1 < _levels.Length && _levels[index + 1].ContentEnabled
                && _levels[index + 1].Level == _levels[index].Level + 1; index++)
                _experienceLimit += _levels[index].NeedExp;
        }

        public int? AddExperience(int current, int amount)
        {
            if (current < 0 || amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            long next = Math.Min(_experienceLimit, (long)current + amount);
            return next <= int.MaxValue ? (int)next : (int?)null;
        }

        public PlayerLevel Resolve(int experience, out int remaining)
        {
            if (experience < 0) throw new ArgumentOutOfRangeException(nameof(experience));
            remaining = experience;
            int index = 0;
            while (index + 1 < _levels.Length && _levels[index + 1].ContentEnabled
                && _levels[index + 1].Level == _levels[index].Level + 1
                && _levels[index].NeedExp > 0 && remaining >= _levels[index].NeedExp)
            {
                remaining -= _levels[index].NeedExp;
                index++;
            }
            if (index + 1 == _levels.Length || !_levels[index + 1].ContentEnabled) remaining = 0;
            return _levels[index];
        }
    }
}
