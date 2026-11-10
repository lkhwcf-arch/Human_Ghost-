using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace HumanGhost.Gameplay
{
    // 원본 저장과 실행 중 지갑이 같은 사전을 공유하지 않습니다.
    public class RewardSnapshot
    {
        public int Experience { get; }
        public IReadOnlyDictionary<int, int> Balances { get; }
        public RewardSnapshot(int experience = 0, IReadOnlyDictionary<int, int> balances = null)
        {
            if (experience < 0) throw new ArgumentOutOfRangeException(nameof(experience));
            Experience = experience;
            var copy = new Dictionary<int, int>();
            if (balances != null)
                foreach (var pair in balances)
                {
                    if (pair.Key <= 0 || pair.Value < 0) throw new ArgumentException("보상 재화의 ID와 수량이 유효하지 않습니다.");
                    copy.Add(pair.Key, pair.Value);
                }
            Balances = new ReadOnlyDictionary<int, int>(copy);
        }
    }
}
