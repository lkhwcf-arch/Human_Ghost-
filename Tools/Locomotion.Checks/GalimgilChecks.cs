using System;
using System.Collections.Generic;
using System.Linq;
using HumanGhost.Data.Definitions;
using HumanGhost.Gameplay;

internal static class GalimgilChecks
{
    public static void Run(Action<bool, string> check)
    {
        GalimgilRewardDefinition Definition(int perKill = 3) => new GalimgilRewardDefinition(1, 0, perKill, 3, false,
            new[] { 8002 }, new[] { new GalimgilGrade("HIGH", 27), new GalimgilGrade("MID", 15), new GalimgilGrade("LOW", 0) });
        CurrencyWallet Wallet(int soul = 0) => new CurrencyWallet(new[] { new Currency(1, 1, 5000, true) }, new Dictionary<int, int> { [1] = soul });
        var kills = Enumerable.Range(1, 30).Select(index => new MonsterSpawnId(8002, 1, index)).ToArray();
        var wallet = Wallet(); var reward = new GalimgilRewards(Definition(), wallet);
        check(reward.Preview(kills).Soul == 90 && reward.Preview(kills).Grade == "HIGH", "산길30마리는 혼90·내부 상위 등급");
        check(reward.Preview(kills.Concat(kills).Append(new MonsterSpawnId(8003, 1, 1))).KillCount == 30,
            "다른 맵·동일 배치 중복 처치는 제외");
        check(reward.RecordCompletedInteraction(101) && !reward.RecordCompletedInteraction(101)
            && reward.Preview(kills).Soul == 93, "성공 오브젝트 ID별 한 번 기록하고 계수 적용");
        int events = 0;
        reward.Granted += quote =>
        {
            events++;
            check(reward.Claimed && wallet.GetBalance(1) == 93 && quote.InteractionCount == 1, "지급 상태 확정 후 통지");
            check(reward.TryGrantAtArrival(kills, out _) == GalimgilGrantResult.Busy, "지급 통지 재진입 차단");
        };
        check(reward.TryGrantAtArrival(kills, out _) == GalimgilGrantResult.Granted && events == 1, "첫 도착 지급");
        check(reward.TryGrantAtArrival(kills, out _) == GalimgilGrantResult.AlreadyClaimed && wallet.GetBalance(1) == 93, "재방문 중복 지급 없음");
        var restored = new GalimgilRewards(Definition(), wallet); restored.Restore(reward.Capture());
        check(restored.Claimed && !restored.RecordCompletedInteraction(101), "저장 사본 복원으로 지급·오브젝트 기록 유지");
        var overflow = new GalimgilRewards(Definition(int.MaxValue), Wallet());
        check(overflow.TryGrantAtArrival(kills, out _) == GalimgilGrantResult.AmountOverflow && !overflow.Claimed, "수량 초과는 지급 기록도 미확정");
        var full = new GalimgilRewards(Definition(), Wallet(int.MaxValue));
        check(full.TryGrantAtArrival(kills, out _) == GalimgilGrantResult.AmountOverflow && !full.Claimed, "지갑 상한 사전 확인");
        var zero = new GalimgilRewards(Definition(), Wallet());
        check(zero.TryGrantAtArrival(Array.Empty<MonsterSpawnId>(), out var empty) == GalimgilGrantResult.Granted
            && empty.Soul == 0 && zero.Claimed, "0 보상도 첫 도착 완료로 처리");
        var failing = new GalimgilRewards(Definition(), Wallet());
        failing.Granted += _ => throw new InvalidOperationException("표시 소비자 오류 검사");
        bool failed = false;
        try { failing.TryGrantAtArrival(kills, out _); } catch (InvalidOperationException) { failed = true; }
        check(failed && failing.Claimed && failing.TryGrantAtArrival(kills, out _) == GalimgilGrantResult.AlreadyClaimed,
            "소비자 오류 후 확정 보상 보존·재지급 없음");
        var progress = new GameProgressState(); progress.ConnectGalimgil(restored); progress.RestartAfterDeath(null);
        check(progress.Galimgil.Claimed && progress.Galimgil.Capture().InteractedObjects.Single() == 101, "사망 시 지급·상호작용 기록 보존");
    }
}
