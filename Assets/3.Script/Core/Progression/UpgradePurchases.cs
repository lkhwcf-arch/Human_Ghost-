using System;

namespace HumanGhost.Gameplay
{
    public enum UpgradePurchaseResult
    {
        Purchased, Busy, UnknownTarget, Disabled, LevelLimit, StepLimit, WrongStep,
        CostUnavailable, CurrencyUnavailable, InsufficientFunds
    }

    // 강화 종류가 달라도 구매 명령·차감·상태 확정·통지 순서는 동일하게 유지합니다.
    public abstract class UpgradePurchases<TKey>
    {
        private readonly CurrencyWallet _wallet;
        private bool _purchasing;
        public event Action<TKey, int> Purchased;

        protected UpgradePurchases(CurrencyWallet wallet)
            => _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));

        protected readonly struct PurchasePlan
        {
            public int CurrencyId { get; }
            public int? Cost { get; }
            public int NextStep { get; }
            public Action Commit { get; }
            public PurchasePlan(int currencyId, int? cost, int nextStep, Action commit)
            { CurrencyId = currencyId; Cost = cost; NextStep = nextStep; Commit = commit; }
        }

        protected abstract UpgradePurchaseResult Prepare(TKey key, out PurchasePlan plan);

        public UpgradePurchaseResult Buy(TKey key)
        {
            if (_purchasing || _wallet.IsChanging) return UpgradePurchaseResult.Busy;
            _purchasing = true;
            try
            {
                UpgradePurchaseResult result = Prepare(key, out var plan);
                if (result != UpgradePurchaseResult.Purchased) return result;
                if (!_wallet.IsEnabled(plan.CurrencyId)) return UpgradePurchaseResult.CurrencyUnavailable;
                if (!plan.Cost.HasValue || plan.Cost.Value < 0) return UpgradePurchaseResult.CostUnavailable;
                if (!_wallet.TrySpend(plan.CurrencyId, plan.Cost.Value, plan.Commit, () => Purchased?.Invoke(key, plan.NextStep)))
                    return UpgradePurchaseResult.InsufficientFunds;
                return UpgradePurchaseResult.Purchased;
            }
            finally { _purchasing = false; }
        }
    }
}
