using System;
using System.Collections.Generic;
using System.Linq;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay;

internal static class RewardChecks
{
    public static void Run(Player definition, Action<bool, string> check)
    {
        var levels = Enumerable.Range(1, 6).Select(level => new PlayerLevel(17000 + level, level,
            level * 40, (level - 1) * 10, (level - 1) * 5, level - 1, level - 1, level <= 5)).ToArray();
        var rules = new PlayerLevels(levels);
        check(rules.Resolve(39, out int left).Level == 1 && left == 39, "필요 경험치 직전에는 레벨 유지");
        check(rules.Resolve(40, out left).Level == 2 && left == 0, "현재 단계의 필요 경험치 소비");
        check(rules.Resolve(120, out left).Level == 3 && left == 0, "여러 레벨 요구량은 누적 소비");
        check(rules.Resolve(1000, out left).Level == 5 && left == 0 && rules.AddExperience(390, 100) == 400,
            "잠긴 다음 레벨에는 진입하지 않고 상한 경험치에서 중지");
        check(rules.Resolve(rules.AddExperience(35, 10).Value, out left).Level == 2 && left == 5,
            "레벨업 초과 경험치는 다음 레벨로 이월");
        var rewards = new MonsterRewards(new[] { new Currency(1, 1, 5000, true), new Currency(2, 2, 5000, true) },
            new[] { new MonsterReward(3101, 3001, 120, 3, 2) }, levels);
        var progress = new GameProgressState();
        progress.ConnectRewards(rewards);
        var spawn = new MonsterSpawnId(8002, 1, 1);
        int events = 0;
        bool reentered = true;
        rewards.Granted += (id, row) =>
        {
            events++;
            check(progress.HasDefeatedMonster(id) && rewards.Experience == 120 && rewards.Wallet.GetBalance(1) == 3
                && rewards.Wallet.GetBalance(2) == 2 && rewards.CurrentLevel.Level == 3, "처치·경험치·혼·엽전은 통지 전에 모두 확정");
            reentered = rewards.TryGrant(new MonsterSpawnId(8002, 1, 2), 3001, progress);
        };
        check(rewards.TryGrant(spawn, 3001, progress) && events == 1 && !reentered, "자동 보상 완료 통지와 중복 진입 차단");
        check(!rewards.TryGrant(spawn, 3001, progress) && events == 1, "같은 배치의 중복 사망 알림은 보상 없음");
        RewardSnapshot saved = rewards.Capture();
        rewards.Restore(new RewardSnapshot(1, new Dictionary<int, int> { [1] = int.MaxValue }));
        var second = new MonsterSpawnId(8002, 1, 2);
        check(!rewards.TryGrant(second, 3001, progress) && rewards.Experience == 1
            && !progress.HasDefeatedMonster(second) && rewards.Wallet.GetBalance(2) == 0, "수량 초과 시 일부 보상과 처치 기록만 확정하지 않음");
        rewards.Restore(saved);
        check(rewards.Experience == 120 && rewards.Wallet.GetBalance(1) == 3, "저장 사본은 이후 실행 상태와 독립");
        var attacks = Enumerable.Range(0, 5).Select(_ => new AttackSpec(1, 1, 0, 0, 0, 0, 0)).ToArray();
        var actor = new PlayerActor(definition, attacks, true, definition.StaminaMax);
        int health = actor.Health;
        actor.ApplyLevel(levels[2]);
        check(actor.MaxHealth == definition.HP + 20 && actor.CombatStats.ATK == definition.ATK + 2
            && actor.CombatStats.DEF == definition.DEF + 2 && actor.Stamina.Max == definition.StaminaMax + 10,
            "레벨별 총 증가량을 원본 능력치에 한 번만 적용");
        actor.ApplyLevel(levels[2]);
        check(actor.Health == health + 20 && actor.Stamina.Current == definition.StaminaMax && actor.MaxHealth == definition.HP + 20,
            "현재 HP는 증가분만 더하고 반복 적용으로 중복 회복하지 않음");
        actor.ConfigureHerbs(10, 0, 3);
        actor.RestoreVitals(actor.MaxHealth, 3);
        check(actor.Health == actor.MaxHealth && definition.HP == health, "강화된 최대 체력 복원과 원본 정의 보존");
        rewards.Restore(new RewardSnapshot(400, new Dictionary<int, int> { [1] = 101, [2] = 3 }));
        progress.RestartAfterDeath(null);
        check(rewards.Wallet.GetBalance(1) == 51 && rewards.Wallet.GetBalance(2) == 2
            && rewards.CurrentLevel.Level == 5 && rewards.LevelExperience == 0, "사망 재화 손실은 버림하고 레벨은 유지");
        var stats = new[] { "Crit", "Dodge", "AttackSpeed", "StaminaRegen", "ParryStamina" }
            .Select(key => new ShrineStatDefinition(key, 1, "검사", true, 4,
                Enumerable.Range(1, 4).Select(step => new KeyValuePair<int, int>(step,
                    key == "AttackSpeed" ? 600 : key == "ParryStamina" ? -3 : 5))));
        var shrine = new ShrineDefinitions(new ShrineLevelDefinition(5, 1, 5000, 1, 10000,
            new Dictionary<int, int>()), stats);
        var upgrades = new[] { new Upgrade(18021, 1, 2, "HERB_CHARGES", 1, 1, 1, true),
            new Upgrade(18031, 1, 2, "HERB_HEAL", 1, 10, 1, true) };
        var session = new UpgradeSession(rewards.Wallet, shrine, upgrades);
        int changes = 0;
        session.EffectsChanged += () => { changes++; actor.ApplyUpgrades(session); };
        check(session.BuyShrine("Crit") == UpgradePurchaseResult.Purchased
            && actor.CombatStats.Crit == definition.Crit + 5 && changes == 1, "공용 구매 완료 때 실제 능력치 갱신");
        check(session.BuyUpgrade(18021) == UpgradePurchaseResult.Purchased
            && actor.HerbCapacity == 4 && actor.HerbCount == 3, "약초 최대 수만 증가하고 구매로 자동 충전하지 않음");
        check(session.BuyUpgrade(18031) == UpgradePurchaseResult.Purchased && session.GetTableValue("HERB_HEAL") == 10,
            "표의 약초 회복량 증가분 합산");
        UpgradeSnapshot upgradeSave = session.Capture();
        session.Restore(upgradeSave);
        check(session.ShrineLevel == 2 && session.GetShrineValue("Crit") == 5
            && session.GetTableValue("HERB_CHARGES") == 1, "강화 저장 사본으로 레벨·단계·효과 복원");
        CurrencyWallet sharedWallet = rewards.Wallet;
        rewards.Restore(new RewardSnapshot(400, new Dictionary<int, int> { [1] = 100, [2] = 100 }));
        check(ReferenceEquals(sharedWallet, rewards.Wallet) && session.BuyShrine("Dodge") == UpgradePurchaseResult.Purchased
            && rewards.Wallet.GetBalance(1) == 99, "복원 후에도 강화와 보상이 같은 지갑 사용");
        check(session.BuyShrine("UnknownEffect") == UpgradePurchaseResult.UnknownTarget
            && rewards.Wallet.GetBalance(1) == 99, "실행 효과가 없는 새 키는 돈만 차감하지 않음");
        var failRewards = new MonsterRewards(new[] { new Currency(1, 1, 0, true), new Currency(2, 2, 0, true) },
            new[] { new MonsterReward(1, 3001, 1000, 2, 1) }, levels);
        var failProgress = new GameProgressState();
        Action<int, int> failedDisplay = (id, amount) => throw new InvalidOperationException("소비자 오류 검사");
        failRewards.Wallet.Changed += failedDisplay;
        bool failed = false;
        try { failRewards.TryGrant(spawn, 3001, failProgress); } catch (InvalidOperationException) { failed = true; }
        check(failed && failRewards.Experience == 400 && failRewards.LevelExperience == 0
            && failRewards.Wallet.GetBalance(2) == 1 && failProgress.HasDefeatedMonster(spawn),
            "소비자 예외 전파 후에도 확정한 전체 보상·처치·레벨 상한 보존");
        failRewards.Wallet.Changed -= failedDisplay;
        check(failRewards.TryGrant(second, 3001, failProgress) && failRewards.Wallet.GetBalance(1) == 4,
            "소비자 오류 뒤에도 처리 잠금은 해제");
        var combo = new PlayerCombo(new[] { new AttackSpec(100, 100, 100, 50, 0, 0, 0), attacks[1], attacks[2], attacks[3], attacks[4] });
        combo.Start(0);
        combo.SetTimeReduction(2400);
        check(combo.Current.TotalTime == 300 && combo.GetAttack(0).TotalTime == 228,
            "강화 도중 진행 중 공격은 유지하고 다음 공격부터 시간 단축");
        combo.Start(0);
        combo.SetTimeReduction(2400);
        check(combo.Current.TotalTime == 228, "같은 공격 속도 적용 반복으로 시간이 계속 줄어들지 않음");
        var menuWallet = new CurrencyWallet(new[] { new Currency(1, 1, 0, true), new Currency(2, 2, 0, true) },
            new Dictionary<int, int> { [1] = 100, [2] = 100 });
        var menuSession = new UpgradeSession(menuWallet, shrine, upgrades);
        var menu = new ShrineMenu(menuSession, shrine, upgrades);
        check(menu.Page == ShrinePage.Main && menu.Count == 3, "서낭당 첫 화면은 세 메뉴");
        menu.Move(-1);
        check(menu.Selected == 2, "목록 처음에서 위로 이동하면 마지막 선택");
        menu.Select(0); menu.Submit();
        check(menu.Page == ShrinePage.Offering && menu.Count == 5 && menuSession.ShrineLevel == 1,
            "혼 메뉴 진입만으로는 구매하지 않음");
        menu.Submit();
        check(menu.Result == UpgradePurchaseResult.Purchased && menuSession.ShrineLevel == 2
            && menuWallet.GetBalance(1) == 99 && menuWallet.GetBalance(2) == 100, "혼 메뉴 구매는 기존 세션으로 혼만 차감");
        menu.Back(); menu.Select(1); menu.Submit();
        check(menu.Page == ShrinePage.Herbs && menu.Count == 2 && menu.NextHerb().ID == 18021
            && menu.HerbMaximum("HERB_CHARGES") == 1, "약초 목록·다음 단계·최대 단계는 표에서 결정");
        menu.Submit();
        check(menu.Result == UpgradePurchaseResult.Purchased && menuWallet.GetBalance(2) == 99, "약초 메뉴는 엽전 구매에 연결");
        menu.Submit();
        check(menu.Result == UpgradePurchaseResult.StepLimit && menuWallet.GetBalance(2) == 99, "상한 반복 결정은 추가 차감 없음");
        menu.Back(); menu.Back(); menu.Move(1);
        check(menu.Closed && menu.Selected == 0, "나가기 뒤 입력으로 선택 상태가 바뀌지 않음");
        var emptyShrine = new ShrineDefinitions(shrine.Level, Array.Empty<ShrineStatDefinition>());
        var empty = new ShrineMenu(new UpgradeSession(menuWallet, emptyShrine, Array.Empty<Upgrade>()), emptyShrine, Array.Empty<Upgrade>());
        empty.Submit(); empty.Submit(); empty.Move(1);
        check(empty.Count == 0 && empty.Result == null, "빈 강화 표에서도 인덱스 오류나 임의 구매 없음");
    }
}
