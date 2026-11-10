using System;
using System.Collections.Generic;
using System.Linq;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay;

internal static class ProgressionChecks
{
    public static void Run(Action<bool, string> check)
    {
        var currencies = new[] { new Currency(1, 1, 5000, true), new Currency(2, 2, 5000, true), new Currency(3, 3, 0, false) };
        ShrineStatDefinition Stat(string key, int maximum, bool enabled = true) => new ShrineStatDefinition(key, 1, "검사 자료", enabled, maximum,
            Enumerable.Range(1, maximum).Select(step => new KeyValuePair<int, int>(step, 5)));
        var definitions = new ShrineDefinitions(new ShrineLevelDefinition(5, 1, 5000, 70, 11500,
            Enumerable.Range(1, 5).Select(level => new KeyValuePair<int, int>(level, 999))),
            new[] { Stat("Alpha", 4), Stat("Beta", 1), Stat("Disabled", 4, false) });
        var costPolicy = new ShrineUpgradeCost(definitions.Level);
        check(Enumerable.Range(1, 4).Select(level => costPolicy.GetCost(level)).SequenceEqual(new int?[] { 70, 80, 92, 106 }),
            "최신 비율식은 기본값에서 계산하고 마지막에만 버림");
        check(costPolicy.GetCost(0) == null && costPolicy.GetCost(5) == null, "범위 밖·최대 레벨에는 구매 비용을 제공하지 않음");
        var changedCost = new ShrineUpgradeCost(new ShrineLevelDefinition(3, 1, 5000, 100, 12000,
            new Dictionary<int, int>()));
        check(changedCost.GetCost(1) == 100 && changedCost.GetCost(2) == 120, "비용 기본값·비율은 데이터에서 공급");
        var overflowCost = new ShrineUpgradeCost(new ShrineLevelDefinition(3, 1, 5000, int.MaxValue, 20000,
            new Dictionary<int, int>()));
        check(overflowCost.GetCost(2) == null, "정수 보유량 범위를 넘는 비용으로 구매하지 않음");
        var initial = new Dictionary<int, int> { [1] = 100, [2] = 25 };
        var formulaWallet = new CurrencyWallet(currencies, new Dictionary<int, int> { [1] = 200 });
        var formulaPurchase = new ShrineUpgrades(definitions, formulaWallet, costPolicy.GetCost);
        check(formulaPurchase.Buy("Alpha") == UpgradePurchaseResult.Purchased
            && formulaPurchase.Buy("Beta") == UpgradePurchaseResult.Purchased && formulaPurchase.Level == 3
            && formulaWallet.GetBalance(1) == 50, "서로 다른 능력치 구매도 최신 계산 정책으로 혼 차감");
        var wallet = new CurrencyWallet(currencies, initial);
        initial[1] = 0;
        check(wallet.GetBalance(1) == 100, "재화 초기 상태는 입력 사전과 분리");
        int changes = 0;
        wallet.Changed += (id, amount) => changes++;
        check(wallet.TryAdd(1, 10) && wallet.GetBalance(1) == 110 && changes == 1, "재화 획득 후 변경 통지");
        check(wallet.TryAdd(1, 0) && changes == 1, "수량 변경 없는 재화는 통지하지 않음");
        check(!wallet.TrySpend(1, 111) && wallet.GetBalance(1) == 110 && changes == 1, "잔액 부족은 상태·통지 유지");
        check(!wallet.TryAdd(3, 1) && !wallet.TryAdd(999, 1), "잠긴 재화·없는 재화 획득 차단");
        check(!wallet.TryAdd(1, int.MaxValue) && wallet.GetBalance(1) == 110, "재화 정수 상한 초과 차단");
        bool negativeFailed = false;
        try { wallet.TryAdd(1, -1); } catch (ArgumentOutOfRangeException) { negativeFailed = true; }
        check(negativeFailed, "음수 획득을 차감으로 처리하지 않음");
        negativeFailed = false;
        try { wallet.TrySpend(1, -1); } catch (ArgumentOutOfRangeException) { negativeFailed = true; }
        check(negativeFailed, "음수 차감을 획득으로 처리하지 않음");
        var captured = wallet.CaptureBalances();
        wallet.TryAdd(1, 10);
        check(captured[1] == 110 && wallet.GetBalance(1) == 120, "재화 수집 사본은 이후 변경과 분리");
        bool readOnly = false;
        try { ((IDictionary<int, int>)captured)[1] = 0; } catch (NotSupportedException) { readOnly = true; }
        check(readOnly, "재화 수집 사본은 읽기 전용");
        var noCost = new ShrineUpgrades(definitions, wallet);
        check(noCost.Buy("Alpha") == UpgradePurchaseResult.CostUnavailable && noCost.Level == 1
            && wallet.GetBalance(1) == 120, "계산식 미연결 시 JSON 비용·무료 구매로 대체하지 않음");
        var levels = new List<int>();
        var purchases = new ShrineUpgrades(definitions, wallet, level => { levels.Add(level); return level * 10; });
        int purchased = 0;
        purchases.Purchased += (key, step) => purchased++;
        bool consistent = false;
        UpgradePurchaseResult nested = UpgradePurchaseResult.Purchased;
        Action<int, int> observe = (id, amount) =>
        {
            consistent = purchases.Level == 2 && purchases.GetStep("Alpha") == 1 && amount == 110;
            nested = purchases.Buy("Beta");
        };
        wallet.Changed += observe;
        check(purchases.Buy("Alpha") == UpgradePurchaseResult.Purchased && consistent && nested == UpgradePurchaseResult.Busy,
            "구매 통지 시 재화·서낭당 레벨·능력치 단계 모두 확정되고 중복 구매 차단");
        wallet.Changed -= observe;
        check(purchases.Buy("Beta") == UpgradePurchaseResult.Purchased && purchases.Level == 3
            && purchases.GetStep("Alpha") == 1 && purchases.GetStep("Beta") == 1 && wallet.GetBalance(1) == 90
            && levels.SequenceEqual(new[] { 1, 2 }) && purchased == 2,
            "다른 능력치 구매도 현재 서낭당 전체 레벨 비용 적용");
        check(purchases.Buy("Beta") == UpgradePurchaseResult.StepLimit && purchases.Buy("Disabled") == UpgradePurchaseResult.Disabled
            && purchases.Buy("Missing") == UpgradePurchaseResult.UnknownTarget && wallet.GetBalance(1) == 90,
            "개별 상한·잠금·없는 능력치 구매는 차감 없음");
        check(purchases.Buy("Alpha") == UpgradePurchaseResult.Purchased && purchases.Buy("Alpha") == UpgradePurchaseResult.Purchased
            && purchases.Level == 5 && wallet.GetBalance(1) == 20, "총 구매마다 서낭당 레벨 상승");
        check(purchases.Buy("Alpha") == UpgradePurchaseResult.LevelLimit && purchases.GetStep("Alpha") == 3,
            "능력치 여유가 남아 있어도 서낭당 전체 상한 적용");
        check(definitions.Level.SoulCosts[1] == 999 && definitions.Stats["Alpha"].Steps[1] == 5, "구매는 원본 정의를 변경하지 않음");
        var steps = purchases.CaptureSteps();
        var restored = new ShrineUpgrades(definitions, new CurrencyWallet(currencies, wallet.CaptureBalances()),
            level => 10, purchases.Level, steps);
        check(restored.Level == 5 && restored.GetStep("Beta") == 1, "수집한 재화·강화 상태로 독립 인스턴스 복원 가능");
        bool invalidProgress = false;
        try { new ShrineUpgrades(definitions, wallet, level: 2); } catch (ArgumentException) { invalidProgress = true; }
        check(invalidProgress, "서낭당 레벨과 능력치 구매 합계 불일치 차단");
        var poorWallet = new CurrencyWallet(currencies);
        var poor = new ShrineUpgrades(definitions, poorWallet, level => 10);
        check(poor.Buy("Alpha") == UpgradePurchaseResult.InsufficientFunds && poor.Level == 1 && poor.GetStep("Alpha") == 0,
            "잔액 부족은 강화 상태도 변경하지 않음");
        var invalidCost = new ShrineUpgrades(definitions, poorWallet, level => -1);
        check(invalidCost.Buy("Alpha") == UpgradePurchaseResult.CostUnavailable, "음수 계산 결과는 사용하지 않음");
        var disabledWallet = new CurrencyWallet(new[] { new Currency(1, 1, 0, false) });
        check(new ShrineUpgrades(definitions, disabledWallet, level => 10).Buy("Alpha") == UpgradePurchaseResult.CurrencyUnavailable,
            "필요 재화가 기획 잠금이면 구매 차단");
        var failingWallet = new CurrencyWallet(currencies, new Dictionary<int, int> { [1] = 100 });
        var committed = new ShrineUpgrades(definitions, failingWallet, level => 10);
        Action<int, int> failedConsumer = (id, amount) => throw new InvalidOperationException("소비자 실패 검사");
        failingWallet.Changed += failedConsumer;
        bool propagated = false;
        try { committed.Buy("Alpha"); } catch (InvalidOperationException) { propagated = true; }
        check(propagated && committed.Level == 2 && committed.GetStep("Alpha") == 1 && failingWallet.GetBalance(1) == 90,
            "구매 완료 후 소비자 오류는 전파하되 확정 상태를 되돌리지 않음");
        failingWallet.Changed -= failedConsumer;
        check(committed.Buy("Alpha") == UpgradePurchaseResult.Purchased && committed.Level == 3,
            "소비자 오류 뒤에도 구매 수명 잠금 해제");

        var coinWallet = new CurrencyWallet(currencies, new Dictionary<int, int> { [1] = 100, [2] = 50 });
        var upgrades = new[] {
            new Upgrade(18021, 1, 2, "HERB_CHARGES", 1, 1, 13, true),
            new Upgrade(18022, 1, 2, "HERB_CHARGES", 2, 1, 19, true),
            new Upgrade(18031, 1, 2, "HERB_HEAL", 1, 10, 5, true),
            new Upgrade(18032, 1, 2, "HERB_HEAL", 2, 10, 8, false) };
        var table = new TableUpgrades(upgrades, coinWallet);
        int tableEvents = 0;
        table.Purchased += (id, step) => tableEvents++;
        check(table.Buy(18022) == UpgradePurchaseResult.WrongStep && coinWallet.GetBalance(2) == 50,
            "엽전 강화는 다음 단계만 구매 가능");
        check(table.Buy(18021) == UpgradePurchaseResult.Purchased && table.GetStep("HERB_CHARGES") == 1
            && coinWallet.GetBalance(2) == 37 && coinWallet.GetBalance(1) == 100 && tableEvents == 1,
            "엽전 약초 강화는 표의 가격으로 엽전만 차감하고 혼 보존");
        check(table.Buy(18021) == UpgradePurchaseResult.WrongStep && tableEvents == 1, "이미 구매한 엽전 강화 중복 차감 없음");
        check(table.Buy(18031) == UpgradePurchaseResult.Purchased && table.GetStep("HERB_CHARGES") == 1
            && table.GetStep("HERB_HEAL") == 1 && coinWallet.GetBalance(2) == 32, "엽전 강화 대상별 단계 독립");
        check(table.Buy(18032) == UpgradePurchaseResult.Disabled && table.Buy(999) == UpgradePurchaseResult.UnknownTarget,
            "잠긴 강화·없는 강화 ID 구매 차단");
        var coinSnapshot = table.CaptureSteps();
        var coinRestored = new TableUpgrades(upgrades, coinWallet, coinSnapshot);
        check(coinRestored.GetStep("HERB_CHARGES") == 1 && coinRestored.GetStep("HERB_HEAL") == 1,
            "엽전 강화 수집 사본으로 독립 복원");
        var sharedShrine = new ShrineUpgrades(definitions, coinWallet, level => 10);
        UpgradePurchaseResult crossPurchase = UpgradePurchaseResult.Purchased;
        Action<int, int> duringPurchase = (id, step) => crossPurchase = sharedShrine.Buy("Alpha");
        table.Purchased += duringPurchase;
        check(table.Buy(18022) == UpgradePurchaseResult.Purchased && crossPurchase == UpgradePurchaseResult.Busy
            && sharedShrine.Level == 1 && coinWallet.GetBalance(1) == 100, "공용 지갑 통지 중 다른 강화의 중복 진입도 차단");
        table.Purchased -= duringPurchase;
        check(upgrades[0].Price == 13 && upgrades[0].Value == 1, "엽전 구매도 원본 Upgrade 정의 보존");
    }
}
