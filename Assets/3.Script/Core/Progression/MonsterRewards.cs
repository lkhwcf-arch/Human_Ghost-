using System;
using System.Collections.Generic;
using System.Linq;
using HumanGhost.Data.Definitions;

namespace HumanGhost.Gameplay
{
    // 화면과 독립된 자동 보상입니다. 레벨 계산은 PlayerLevels에 위임하며 랜덤박스는 아직 연결하지 않습니다.
    public class MonsterRewards
    {
        private const int _soulCurrencyId = 1;
        private const int _coinCurrencyId = 2;
        public static int SoulCurrencyId => _soulCurrencyId;
        public static int CoinCurrencyId => _coinCurrencyId;
        private readonly Currency[] _currencies;
        private readonly Dictionary<int, MonsterReward> _rewards;
        private readonly PlayerLevels _levels;
        private bool _granting;
        public CurrencyWallet Wallet { get; private set; }
        public int Experience { get; private set; }
        public PlayerLevel CurrentLevel => _levels.Resolve(Experience, out _);
        public int LevelExperience { get { _levels.Resolve(Experience, out int remaining); return remaining; } }
        public event Action<MonsterSpawnId, MonsterReward> Granted;
        public event Action<int, int> LeveledUp;
        public event Action<ExecutionRewardPlan> ExecutionGranted;

        public MonsterRewards(IEnumerable<Currency> currencies, IEnumerable<MonsterReward> rewards, IEnumerable<PlayerLevel> levels)
        {
            if (currencies == null) throw new ArgumentNullException(nameof(currencies));
            if (rewards == null) throw new ArgumentNullException(nameof(rewards));
            _currencies = currencies.ToArray();
            _rewards = rewards.ToDictionary(row => row.MonsterID);
            _levels = new PlayerLevels(levels);
            Restore(new RewardSnapshot());
        }

        public RewardSnapshot Capture() => new RewardSnapshot(Experience, Wallet.CaptureBalances());
        // 저장 복원은 게임 진입 준비 중, 소비자가 연결되기 전에만 실행합니다.
        public void Restore(RewardSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (_granting) throw new InvalidOperationException("보상 통지 중에는 저장 상태를 복원할 수 없습니다.");
            if (Wallet == null) Wallet = new CurrencyWallet(_currencies, snapshot.Balances);
            else Wallet.Restore(snapshot.Balances);
            Experience = _levels.AddExperience(snapshot.Experience, 0)
                ?? throw new ArgumentException("저장 경험치가 처리 범위를 넘습니다.", nameof(snapshot));
        }

        public bool TryGrant(MonsterSpawnId spawn, int monsterId, GameProgressState progress)
            => TryGrant(spawn, monsterId, progress, null, null);

        // 실제 성공 처형만 execution을 전달합니다. 일반 사망의 기존 보상도 같은 지급 안에서 처리합니다.
        public bool TryGrant(MonsterSpawnId spawn, int monsterId, GameProgressState progress,
            ExecutionRewardSession execution, PlayerActor recipient)
        {
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            if (_granting) return false;
            if (progress.HasDefeatedMonster(spawn)) return false;
            if (!_rewards.TryGetValue(monsterId, out var reward))
                throw new InvalidOperationException($"몬스터 {monsterId}의 보상 데이터가 없습니다.");
            int? experience = _levels.AddExperience(Experience, reward.Exp);
            if (!experience.HasValue) return false;
            ExecutionRewardPlan plan = null;
            if (execution != null)
            {
                if (recipient == null) throw new ArgumentNullException(nameof(recipient));
                if (!execution.TryPrepare(spawn, monsterId, out plan)) return false;
                if (plan.Reward.DropItemID != 0)
                    throw new InvalidOperationException("처형 아이템 지급은 인벤토리 연결이 필요합니다. 보상을 확정하지 않습니다.");
            }
            long coins = (long)reward.CoinDrop + (plan?.Reward.CoinDrop ?? 0);
            if (coins > int.MaxValue) return false;
            bool healed = false;
            int beforeLevel = CurrentLevel.Level;
            if (reward.SoulDrop < 0 || reward.CoinDrop < 0) throw new InvalidOperationException("보상 수량은 음수일 수 없습니다.");
            _granting = true;
            try
            {
                return Wallet.TryApply(new Dictionary<int, int>
                    { [_soulCurrencyId] = reward.SoulDrop, [_coinCurrencyId] = (int)coins },
                    () =>
                    {
                        if (plan != null && !execution.TryCommit(plan))
                            throw new InvalidOperationException("처형 보상 계획이 변경되어 지급을 중단했습니다.");
                        Experience = experience.Value; progress.MarkMonsterDefeated(spawn);
                        if (plan != null) healed = recipient.ApplyExecutionHealing(plan.Reward.HealHP);
                    },
                    () =>
                    {
                        int afterLevel = CurrentLevel.Level;
                        if (afterLevel > beforeLevel) LeveledUp?.Invoke(beforeLevel, afterLevel);
                        if (healed) recipient.NotifyExecutionHealing();
                        Granted?.Invoke(spawn, reward);
                        if (plan != null) ExecutionGranted?.Invoke(plan);
                    });
            }
            finally { _granting = false; }
        }

        public bool ApplyDeathLoss()
        {
            if (_granting) return false;
            var losses = _currencies.Where(row => row.ContentEnabled).ToDictionary(row => row.ID,
                row => -(int)((long)Wallet.GetBalance(row.ID) * row.DeathLossRate / 10000));
            return Wallet.TryApply(losses, () => { }, () => { });
        }
    }
}
