using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace HumanGhost.Gameplay
{
    public class UpgradeSnapshot
    {
        public int ShrineLevel { get; }
        public IReadOnlyDictionary<string, int> ShrineSteps { get; }
        public IReadOnlyDictionary<string, int> TableSteps { get; }
        public UpgradeSnapshot(int shrineLevel = 1, IReadOnlyDictionary<string, int> shrineSteps = null,
            IReadOnlyDictionary<string, int> tableSteps = null)
        {
            if (shrineLevel < 1) throw new ArgumentOutOfRangeException(nameof(shrineLevel));
            ShrineLevel = shrineLevel;
            ShrineSteps = Copy(shrineSteps);
            TableSteps = Copy(tableSteps);
        }
        private static IReadOnlyDictionary<string, int> Copy(IReadOnlyDictionary<string, int> source)
        {
            var copy = new Dictionary<string, int>(StringComparer.Ordinal);
            if (source != null) foreach (var pair in source)
            {
                if (string.IsNullOrEmpty(pair.Key) || pair.Value < 0) throw new ArgumentException("강화 키와 단계가 유효하지 않습니다.");
                copy.Add(pair.Key, pair.Value);
            }
            return new ReadOnlyDictionary<string, int>(copy);
        }
    }
}
