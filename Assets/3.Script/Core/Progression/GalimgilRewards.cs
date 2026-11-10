using System;
using System.Collections.Generic;
using System.Linq;
using HumanGhost.Data.Definitions;

namespace HumanGhost.Gameplay
{
    public enum GalimgilGrantResult { Granted, AlreadyClaimed, Busy, CurrencyUnavailable, AmountOverflow }
    public readonly struct GalimgilRewardQuote
    {
        public int KillCount { get; }
        public int InteractionCount { get; }
        public long Soul { get; }
        public string Grade { get; }
        public GalimgilRewardQuote(int kills, int interactions, long soul, string grade)
        { KillCount = kills; InteractionCount = interactions; Soul = soul; Grade = grade; }
    }

    // 별도 처치 카운터 없이 기존 배치 ID 기록을 집계하고 공용 지갑에 한 번 지급합니다.
    public class GalimgilRewards
    {
        private readonly GalimgilRewardDefinition _definition;
        private readonly CurrencyWallet _wallet;
        private readonly HashSet<int> _stages;
        private readonly HashSet<int> _interacted = new HashSet<int>();
        private bool _granting;
        public bool Claimed { get; private set; }
        public event Action<GalimgilRewardQuote> Granted;
        public GalimgilRewards(GalimgilRewardDefinition definition, CurrencyWallet wallet)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _stages = new HashSet<int>(definition.CountStages);
        }
        public bool RecordCompletedInteraction(int objectId)
        {
            if (objectId <= 0) throw new ArgumentOutOfRangeException(nameof(objectId));
            return !_granting && !_wallet.IsChanging && _interacted.Add(objectId);
        }
        public GalimgilRewardQuote Preview(IEnumerable<MonsterSpawnId> defeated)
        {
            int kills = defeated.Where(id => _stages.Contains(id.StageId)).Distinct().Count();
            long soul = _definition.BaseSoul + (long)kills * _definition.SoulPerKill + (long)_interacted.Count * _definition.SoulPerInteraction;
            return new GalimgilRewardQuote(kills, _interacted.Count, soul,
                _definition.Grades.First(row => kills >= row.MinKillCount).Key);
        }
        public GalimgilGrantResult TryGrantAtArrival(IEnumerable<MonsterSpawnId> defeated, out GalimgilRewardQuote quote)
        {
            quote = default;
            if (_granting || _wallet.IsChanging) return GalimgilGrantResult.Busy;
            if (Claimed) return GalimgilGrantResult.AlreadyClaimed;
            if (!_wallet.IsEnabled(_definition.SoulCurrencyId)) return GalimgilGrantResult.CurrencyUnavailable;
            quote = Preview(defeated);
            if (quote.Soul < 0 || quote.Soul > int.MaxValue
                || quote.Soul + _wallet.GetBalance(_definition.SoulCurrencyId) > int.MaxValue) return GalimgilGrantResult.AmountOverflow;
            GalimgilRewardQuote completed = quote;
            _granting = true;
            try
            {
                bool granted = _wallet.TryApply(new Dictionary<int, int> { [_definition.SoulCurrencyId] = (int)quote.Soul },
                    () => Claimed = true, () => Granted?.Invoke(completed));
                return granted ? GalimgilGrantResult.Granted : GalimgilGrantResult.Busy;
            }
            finally { _granting = false; }
        }
        public GalimgilSnapshot Capture() => new GalimgilSnapshot(Claimed ? 1 : 0, _interacted);
        public void Restore(GalimgilSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (_granting || _wallet.IsChanging) throw new InvalidOperationException("보상 통지 중에는 갈림길 기록을 복원할 수 없습니다.");
            Claimed = snapshot.Claimed == 1; _interacted.Clear(); _interacted.UnionWith(snapshot.InteractedObjects);
        }
    }
}
