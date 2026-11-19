using System;
using HumanGhost.Gameplay;

internal static class GameProgressChecks
{
    public static void Run(Action<bool, string> check)
    {
        var first = new GameProgressState();
        var second = new GameProgressState();
        var id = new MonsterSpawnId(8002, 101, 1);
        first.OpenGate(8101); first.MarkMonsterDefeated(id); first.ActivateShrine(8002); first.MarkCutsceneViewed(18001);
        check(!second.HasOpenedGate(8101) && !second.HasDefeatedMonster(id) && second.ShrineId == 0, "새 게임의 진행 상태는 다른 세션과 분리");
        int[] gates = first.OpenedGates; gates[0] = 9999;
        check(first.HasOpenedGate(8101) && !first.HasOpenedGate(9999), "조회 사본으로 원본 진행 목록을 변경할 수 없음");
        first.Travel(8003, "Arrival_Test", 70, 2);
        check(first.CurrentStage == 8003 && first.TakeArrival() == "Arrival_Test" && first.TakeArrival() == null
            && first.CarriedHealth == 70, "이동 상태 전달과 도착 요청 한 번 소비");
        first.ClearCarriedVitals();
        check(first.CarriedHealth == null && first.CarriedHerbs == 0, "전달한 자원은 소비 후 정리");
        var saved = new SaveSnapshot(8002, 8002, 0, 0, 100, 2, new[] { 8001, 8004 }, new[] { 8002 },
            new[] { 8101 }, new[] { 18001 }, Array.Empty<int>(), new[] { id });
        second.Restore(saved);
        check(second.HasDefeatedMonster(id) && second.HasDefeatedBoss(8004) && second.Progress.IsCleared(8001)
            && second.HasActiveShrine(8002), "저장 진행 상태 복원");
        var unsaved = new MonsterSpawnId(8002, 101, 2);
        second.MarkMonsterDefeated(unsaved);
        second.RestartAfterDeath(saved, TestDefinitions.Stages());
        check(second.HasDefeatedMonster(id) && !second.HasDefeatedMonster(unsaved) && second.CurrentStage == 8002
            && second.TakeArrival() == null, "사망 후 처치 기록 복원과 현재 맵 기본 입구 복귀");
        second.RestartAfterDeath(null, TestDefinitions.Stages());
        check(second.DefeatedMonsters.Length == 0 && second.CarriedHealth == null, "저장 없는 사망 처리도 이전 임시 처치를 남기지 않음");
        var early = new GameProgressState();
        early.RestartAfterDeath(null, TestDefinitions.Stages());
        check(early.CurrentStage == 8001, "장부 조사 전에는 기존 시작 위치 유지");
        early.Progress.Clear(8005);
        early.ActivateShrine(8005);
        early.RestartAfterDeath(null, TestDefinitions.Stages());
        check(early.CurrentStage == 8002 && early.ArrivalAnchor == null, "조사 완료 뒤 장부방 체크포인트 대신 다음 맵 시작점");
        early.ActivateShrine(8003);
        early.RestartAfterDeath(null, TestDefinitions.Stages());
        check(early.CurrentStage == 8002 && early.ArrivalAnchor == null, "필드 사망은 다른 맵 서낭당으로 보내지 않음");
        first.RestartAfterDeath(saved, TestDefinitions.Stages());
        check(first.CurrentStage == 8003 && first.ArrivalAnchor == "Arrival_Test" && first.StageEntrance == "Arrival_Test",
            "다른 맵 저장·서낭당과 무관하게 죽은 맵의 실제 도착 입구 유지");
        first.TakeArrival(); first.RestartAfterDeath(saved, TestDefinitions.Stages());
        check(first.ArrivalAnchor == "Arrival_Test", "두 번 죽어도 도착 요청 소비와 입구 기록 분리");
        first.Travel(8004, "Boss_Entrance", 100, 3); first.ActivateShrine(8007);
        first.RestartAfterDeath(saved, TestDefinitions.Stages());
        check(first.CurrentStage == 8007 && first.ArrivalAnchor == "Respawn_Seonangdang" && first.StageEntrance == null,
            "보스방 사망은 마지막 서낭당 앞·다른 맵 입구 유출 없음");
        bool invalid = false; try { first.SetStageEntrance(" "); } catch (ArgumentException) { invalid = true; }
        check(invalid, "빈 입구 앵커 거부");

        // 맵 역할은 데이터(Stage.Role)가 정합니다. 맵 ID를 바꿔도 같은 규칙이 적용됩니다(조건 규칙 감사 3.8).
        StageRoles stages = TestDefinitions.Stages();
        check(stages.VillageStage == 8001 && stages.LedgerStage == 8005 && stages.StageAfterLedger == 8002 && stages.FirstBossStage == 8004
            && stages.IsBoss(8004) && stages.IsField(8003) && !stages.IsField(8005) && !stages.IsBoss(9999), "Stage.Role로 마을·장부방·보스방·일반 맵 판정");
        var moved = new StageRoles(new[] { TestDefinitions.StageRow(7001, 7002, "VILLAGE"), TestDefinitions.StageRow(7002, null, "FIELD"),
            TestDefinitions.StageRow(7003, null, "LEDGER"), TestDefinitions.StageRow(7009, null, "BOSS"), TestDefinitions.StageRow(7010, null, "BOSS") });
        var renumbered = new GameProgressState(); renumbered.Travel(7010, "Boss_Entrance", 100, 3);
        renumbered.RestartAfterDeath(null, moved);
        check(renumbered.CurrentStage == 7001 && moved.FirstBossStage == 7009, "맵 ID를 바꾸거나 보스방을 늘려도 코드 수정 없이 역할로 사망 복귀");
        bool noVillage = false;
        try { new StageRoles(new[] { TestDefinitions.StageRow(8005, null, "LEDGER"), TestDefinitions.StageRow(8004, null, "BOSS") }); }
        catch (InvalidOperationException) { noVillage = true; }
        check(noVillage, "VILLAGE 역할 맵이 없으면 조립 실패");
    }
}
