using System;
using System.Linq;
using System.Threading.Tasks;
using HumanGhost.Gameplay;

internal static class SaveChecks
{
    public static void Run(Action<bool, string> check) => RunAsync(check).GetAwaiter().GetResult();

    private static async Task RunAsync(Action<bool, string> check)
    {
        var store = new SaveTestStore();
        long time = 1000;
        SaveSession Create(SaveBuildMode mode, params SaveRecord[] records) =>
            new SaveSession(mode, 1, store, () => ++time, records);
        SaveSnapshot Snapshot(int x = 125) => new SaveSnapshot(8001, 0, x, -250, 70, 2,
            new[] { 8001 }, Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>(), new[] { 9001 });
        var session = Create(SaveBuildMode.Demo);
        var oldSnapshot = new SaveSnapshot(8002, 8001, 350, -125, 60, 3, new[] { 8001 }, new[] { 8001 },
            new[] { 800201, 9001 }, new[] { 18001 }, new[] { 9002 }, new[] { new MonsterSpawnId(8002, 101, 1) });
        var oldRecord = new SaveRecord(1, 7, 500, oldSnapshot, 12);
        var compatible = oldRecord.WithExistingGates(id => id == 9001);
        check(compatible.Snapshot.OpenedGates.SequenceEqual(new[] { 9001 }), "현재 표에 없는 관문만 복원 대상에서 제외");
        check(oldRecord.Snapshot.OpenedGates.SequenceEqual(new[] { 9001, 800201 })
            && !ReferenceEquals(oldRecord, compatible) && !ReferenceEquals(oldSnapshot, compatible.Snapshot),
            "관문 복원 호환은 원본 기록과 스냅샷을 변경하지 않음");
        check(compatible.Version == 1 && compatible.Slot == 7 && compatible.SavedAtUnixMs == 500 && compatible.SaveOrder == 12,
            "관문 호환 처리 후 슬롯·버전·시각·정렬 순서 보존");
        check(compatible.Snapshot.StageId == 8002 && compatible.Snapshot.ShrineId == 8001
            && compatible.Snapshot.PositionX == 350 && compatible.Snapshot.PositionY == -125
            && compatible.Snapshot.Hp == 60 && compatible.Snapshot.Herbs == 3, "관문 호환 처리 후 위치·자원 보존");
        check(compatible.Snapshot.ClearedStages.SequenceEqual(oldSnapshot.ClearedStages)
            && compatible.Snapshot.ActiveShrines.SequenceEqual(oldSnapshot.ActiveShrines)
            && compatible.Snapshot.ViewedCutscenes.SequenceEqual(oldSnapshot.ViewedCutscenes)
            && compatible.Snapshot.UsedSavePoints.SequenceEqual(oldSnapshot.UsedSavePoints)
            && compatible.Snapshot.DefeatedMonsters.SequenceEqual(oldSnapshot.DefeatedMonsters), "관문 외 진행·자동 저장 이력·개별 처치 보존");
        check(oldRecord.WithExistingGates(id => false).Snapshot.OpenedGates.Count == 0, "관문 표가 비어 있어도 이전 기록 복원 허용");
        check(oldRecord.WithExistingGates(id => true).Snapshot.OpenedGates.SequenceEqual(oldSnapshot.OpenedGates),
            "현재 관문이 남아 있으면 열린 상태 유지");
        var compatibleSession = Create(SaveBuildMode.Demo, compatible);
        check(compatibleSession.TryContinue(7, out var gateRestored) && gateRestored.OpenedGates.SequenceEqual(new[] { 9001 }),
            "최신·선택 기록에 공용 호환 사본으로 세션 진입");
        bool missingLookup = false;
        try { oldRecord.WithExistingGates(null); } catch (ArgumentNullException) { missingLookup = true; }
        check(missingLookup, "관문 조회 계약 누락을 전체 관문 폐기로 오해하지 않음");
        int events = 0;
        session.Saved += (record, kind) => events++;
        check(!session.TryContinue(out _), "기록 없이 이어하기 불가");
        check(await session.NewGameAsync(Snapshot()), "새 게임 즉시 초기 기록 저장");
        check(session.CurrentSlot == 1 && session.GetRecords()[0].Snapshot.UsedSavePoints.Count == 0,
            "새 게임은 첫 빈 슬롯을 사용하고 자동 저장 지점 이력 초기화");
        check(session.TryContinue(out var restored) && restored.PositionX == 125 && restored.PositionY == -250
            && restored.Hp == 70 && restored.Herbs == 2, "이어하기는 저장 당시 위치와 자원 반환");
        check(await session.SaveManualAsync(7, false, Snapshot(777)) && session.CurrentSlot == 7,
            "수동 저장은 선택한 슬롯 사용");
        check(!await session.SaveManualAsync(7, false, Snapshot()), "기존 슬롯은 덮어쓰기 승인 필요");
        check(await session.SaveAutomaticAsync(9001, Snapshot()), "자동 저장 최초 지점 처리");
        check(!await session.SaveAutomaticAsync(9001, Snapshot()) && session.GetRecords().Count == 2,
            "자동 저장은 현재 슬롯만 갱신하고 지점 재방문 무시");
        var kills = new[] { new MonsterSpawnId(8002, 101, 1), new MonsterSpawnId(8002, 101, 2) };
        var killedSnapshot = new SaveSnapshot(8002, 8002, 10, 20, 70, 2, Array.Empty<int>(), new[] { 8002 },
            Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>(), kills);
        kills[0] = new MonsterSpawnId(8003, 101, 1);
        check(killedSnapshot.DefeatedMonsters[0].Equals(new MonsterSpawnId(8002, 101, 1)),
            "처치 스냅샷은 원본 목록을 복사하고 맵·배치·개체 순번을 구분");
        check(await session.SaveAutomaticAsync(9001, killedSnapshot, true)
            && session.CurrentSnapshot.DefeatedMonsters.Count == 2 && session.GetRecords().Count == 2,
            "명시적인 서낭당 휴식은 같은 현재 슬롯에 반복 저장하고 처치 기록 보존");
        var reloaded = Create(SaveBuildMode.Demo, session.GetRecords().ToArray());
        check(reloaded.TryContinue(out _) && reloaded.CurrentSlot == 7
            && !await reloaded.SaveAutomaticAsync(9001, Snapshot()), "재실행 후에도 자동 저장 지점 이력 유지");
        check(await reloaded.SaveManualAsync(3, false, Snapshot())
            && await reloaded.SaveAutomaticAsync(9002, Snapshot()), "수동 저장으로 현재 슬롯을 옮겨도 지점 이력 유지");
        check(reloaded.GetRecords()[0].Snapshot.UsedSavePoints.SequenceEqual(new[] { 9001, 9002 }),
            "저장 성공 이력만 누적");

        var before = session.GetRecords()[0];
        store.Fail = true;
        try { await session.SaveAutomaticAsync(9003, Snapshot()); }
        catch (InvalidOperationException) { }
        check(!session.IsSaving && ReferenceEquals(before, session.GetRecords()[0]) && events == 4,
            "기록 실패는 기존 슬롯과 성공 이벤트를 변경하지 않음");
        store.Fail = false;
        check(await session.SaveAutomaticAsync(9003, Snapshot()), "실패한 자동 저장 지점 재시도 가능");

        store.Pending = new TaskCompletionSource<bool>();
        Task<bool> pending = session.SaveAutomaticAsync(9004, Snapshot());
        check(session.IsSaving && !await session.SaveAutomaticAsync(9004, Snapshot())
            && !await session.SaveManualAsync(4, false, Snapshot()) && !await session.NewGameAsync(Snapshot())
            && !session.TryContinue(out _), "기록 중 모든 저장과 이어하기 중복 실행 차단");
        store.Pending.SetResult(true);
        check(await pending && !session.IsSaving, "비동기 기록 완료 후 요청 잠금 해제");
        store.Pending = null;

        var full = Create(SaveBuildMode.Demo, Enumerable.Range(1, 10)
            .Select(slot => new SaveRecord(1, slot, slot * 10, Snapshot())).ToArray());
        check(await full.NewGameAsync(Snapshot()) && full.CurrentSlot == 1 && full.GetRecords().Count == 10,
            "새 게임에서 가득 찬 슬롯은 가장 오래된 기록 교체");
        check(await full.SaveManualAsync(5, true, Snapshot()) && full.GetRecords()[0].Slot == 5,
            "수동 덮어쓰기는 지정 슬롯만 교체하고 최신 날짜순 정렬");

        var release = Create(SaveBuildMode.Release);
        check(release.Capacity == 1 && !release.ManualSaveEnabled && await release.NewGameAsync(Snapshot()),
            "출시 정책은 한 슬롯 사용");
        check(!await release.SaveManualAsync(1, true, Snapshot()) && await release.SaveAutomaticAsync(9001, Snapshot())
            && release.GetRecords().Count == 1, "출시 정책은 수동 저장 요청을 거부하고 자동 저장만 허용");

        var observed = Create(SaveBuildMode.Demo);
        observed.Saved += (record, kind) => throw new ApplicationException("표시 소비자 검사 오류");
        try { await observed.NewGameAsync(Snapshot()); }
        catch (ApplicationException) { }
        check(observed.GetRecords().Count == 1 && !observed.IsSaving, "성공 소비자 오류와 저장 실패 경계 분리");

        var sameTime = new SaveSession(SaveBuildMode.Demo, 1, store, () => 2000, Array.Empty<SaveRecord>());
        await sameTime.NewGameAsync(Snapshot());
        await sameTime.SaveManualAsync(9, false, Snapshot(900));
        check(sameTime.TryContinue(out var latest) && latest.PositionX == 900,
            "같은 밀리초에 기록해도 기록 순서로 최신 슬롯 구분");
        check(sameTime.TryContinue(1, out var selected) && selected.PositionX == 125 && sameTime.CurrentSlot == 1,
            "목록 표시 순서와 무관하게 선택한 내부 슬롯으로 이어하기");
        check(!sameTime.TryContinue(5, out _) && sameTime.CurrentSlot == 1,
            "없는 슬롯 선택은 현재 플레이 슬롯을 변경하지 않음");
    }

}

// 슬롯 검사와 복원 흐름 검사가 같은 저장 실패·대기 구현을 사용합니다.
internal sealed class SaveTestStore : ISaveStore
{
    public bool Fail { get; set; }
    public TaskCompletionSource<bool> Pending { get; set; }

    public Task WriteAsync(SaveRecord record)
    {
        if (Fail) throw new InvalidOperationException("검사용 저장 실패");
        return Pending?.Task ?? Task.CompletedTask;
    }
}
