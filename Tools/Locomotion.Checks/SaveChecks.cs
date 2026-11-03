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
        check(!session.IsSaving && ReferenceEquals(before, session.GetRecords()[0]) && events == 3,
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
