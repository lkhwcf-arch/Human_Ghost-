using System;
using System.Collections.Generic;
using System.Linq;

namespace HumanGhost.Data.Definitions
{
    // 검사 완료된 콘텐츠 정의입니다. 실행 집계와 지급 기록은 포함하지 않습니다.
    public class GalimgilRewardDefinition
    {
        public int SoulCurrencyId { get; }
        public int BaseSoul { get; }
        public int SoulPerKill { get; }
        public int SoulPerInteraction { get; }
        public bool ShowGrade { get; }
        public IReadOnlyList<int> CountStages { get; }
        public IReadOnlyList<GalimgilGrade> Grades { get; }
        public GalimgilRewardDefinition(int soulCurrencyId, int baseSoul, int soulPerKill, int soulPerInteraction,
            bool showGrade, IEnumerable<int> countStages, IEnumerable<GalimgilGrade> grades)
        {
            SoulCurrencyId = soulCurrencyId; BaseSoul = baseSoul; SoulPerKill = soulPerKill;
            SoulPerInteraction = soulPerInteraction; ShowGrade = showGrade;
            CountStages = Array.AsReadOnly(countStages.ToArray());
            Grades = Array.AsReadOnly(grades.ToArray());
        }
    }
    public class GalimgilGrade
    {
        public string Key { get; }
        public int MinKillCount { get; }
        public GalimgilGrade(string key, int minKillCount) { Key = key; MinKillCount = minKillCount; }
    }
}
