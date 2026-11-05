using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HumanGhost.Gameplay;

internal static class SaveGameFlowChecks
{
    public static void Run(Action<bool, string> check) => RunAsync(check).GetAwaiter().GetResult();

    private static async Task RunAsync(Action<bool, string> check)
    {
        var snapshot = new SaveSnapshot(8003, 0, 350, -125, 70, 2,
            Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>(), new[] { 9001 });
        var runtime = new TestRuntime(snapshot);
        var store = new SaveTestStore();
        var session = new SaveSession(SaveBuildMode.Demo, 1, store, () => 2000,
            new[] { new SaveRecord(1, 7, 1000, snapshot) });
        var flow = new SaveGameFlow(session, runtime);
        check(!flow.ResumePrepared(), "현재 슬롯 없이 맵 이동 재개 거부" );
        int ready = 0;
        flow.Ready += state => ready++;
        check(!await flow.SaveManualAsync(1, false) && runtime.Captures == 0, "복원 준비 전에는 상태 수집과 저장을 실행하지 않음");
        check(!await flow.ContinueAsync(2) && runtime.Order.Count == 0, "없는 슬롯은 맵 준비 요청 없이 거부");
        runtime.Pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> loading = flow.ContinueAsync(7);
        check(flow.IsBusy && !flow.IsPlayable && session.CurrentSlot == null
            && runtime.Order.SequenceEqual(new[] { "진행 차단", "준비" }), "준비 완료 전에 상태 적용과 현재 슬롯 확정을 하지 않음");
        check(!await flow.NewGameAsync(snapshot) && !await flow.SaveAutomaticAsync(9002), "복원 중 새 게임과 저장 중복 실행 거부");
        runtime.Pending.SetResult(true);
        check(await loading && flow.IsPlayable && runtime.Playable && session.CurrentSlot == 7 && ready == 1,
            "준비·적용 후 선택 슬롯 확정과 게임 진행 허용");
        check(runtime.Order.SequenceEqual(new[] { "진행 차단", "준비", "적용", "진행 허용" })
            && ReferenceEquals(runtime.Applied, snapshot), "맵 없이도 상태 사본과 복원 순서를 검사 가능");
        runtime.Pending = null;
        var nextRuntime = new TestRuntime(snapshot);
        var nextFlow = new SaveGameFlow(session, nextRuntime);
        check(nextFlow.ResumePrepared() && nextFlow.IsPlayable && nextRuntime.Applies == 0
            && nextRuntime.Captures == 0 && session.CurrentSlot == 7,
            "일반 맵 이동은 마지막 저장 위치를 재적용하거나 새 기록을 쓰지 않음");
        check(session.PendingWrite.IsCompleted, "저장 전에는 대기할 파일 기록 작업이 없음");
        check(await flow.SaveManualAsync(4, false) && runtime.Captures == 1 && session.CurrentSlot == 4,
            "수동 저장은 주입한 수집 담당과 기존 슬롯 정책 사용");
        check(await flow.SaveAutomaticAsync(9002) && runtime.Captures == 2 && session.CurrentSlot == 4,
            "자동 저장도 같은 상태 수집 절차 사용");

        using var cancellation = new CancellationTokenSource();
        runtime.Pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int applies = runtime.Applies;
        loading = flow.ContinueAsync(7, cancellation.Token);
        cancellation.Cancel();
        runtime.Pending.SetResult(true);
        bool cancelled = false;
        try { await loading; }
        catch (OperationCanceledException) { cancelled = true; }
        check(cancelled && !flow.IsBusy && !flow.IsPlayable && !runtime.Playable && runtime.Applies == applies
            && session.CurrentSlot == 4 && ready == 1, "늦은 맵 준비 결과는 취소된 상태와 슬롯·입력을 변경하지 않음");
        runtime.Pending = null;
        runtime.FailPrepare = true;
        bool failed = false;
        try { await flow.ContinueAsync(7); }
        catch (InvalidOperationException) { failed = true; }
        check(failed && !flow.IsBusy && !runtime.Playable && session.CurrentSlot == 4, "준비 실패 시 진행 차단과 기존 슬롯 유지");
        runtime.FailPrepare = false;
        runtime.FailApply = true;
        failed = false;
        try { await flow.ContinueAsync(7); }
        catch (InvalidOperationException) { failed = true; }
        check(failed && !runtime.Playable && session.CurrentSlot == 4 && ready == 1, "상태 적용 실패는 슬롯 확정과 준비 완료를 알리지 않음");
        runtime.FailApply = false;
        check(await flow.ContinueAsync() && session.CurrentSlot == 4, "실패 후 최신 기록으로 재시도 가능");

        store.Fail = true;
        int count = session.GetRecords().Count;
        failed = false;
        try { await flow.NewGameAsync(snapshot); }
        catch (InvalidOperationException) { failed = true; }
        check(failed && !runtime.Playable && !flow.IsBusy && session.GetRecords().Count == count
            && session.CurrentSlot == 4, "새 게임 초기 기록 실패 시 플레이를 허용하거나 기존 슬롯을 교체하지 않음");
        store.Fail = false;
        check(await flow.NewGameAsync(snapshot) && runtime.Applied.UsedSavePoints.Count == 0
            && session.GetRecords()[0].Snapshot.UsedSavePoints.Count == 0, "새 게임은 적용할 상태와 기록의 지점 이력을 함께 초기화");

        var release = new SaveGameFlow(new SaveSession(SaveBuildMode.Release, 1, store, () => 3000,
            Array.Empty<SaveRecord>()), runtime);
        await release.NewGameAsync(snapshot);
        int captures = runtime.Captures;
        check(!await release.SaveManualAsync(1, true) && runtime.Captures == captures, "출시 수동 저장은 상태 수집 전 차단");
        check(!release.TryOpenManualSave(), "출시 정책은 수동 저장 창 열기도 차단");

        check(flow.TryOpenManualSave() && flow.IsManualSaveOpen && !flow.IsPlayable && !runtime.Playable,
            "수동 저장 창 열기는 게임 진행을 차단");
        check(!flow.TryOpenManualSave() && !await flow.ContinueAsync(7) && !await flow.NewGameAsync(snapshot)
            && !await flow.SaveAutomaticAsync(9004), "수동 저장 창에서 중복 열기·복원·자동 저장 차단");
        var slots = flow.GetSlots(true);
        check(slots.Count == 10 && slots.Take(3).All(entry => entry.Record != null)
            && slots.Skip(3).All(entry => entry.Record == null) && slots.Select(entry => entry.Slot).Distinct().Count() == 10,
            "날짜순 기록 다음에 빈 칸을 내부 슬롯 중복 없이 구성");
        int target = slots.First(entry => entry.Record == null).Slot;
        check(await flow.SaveManualAsync(target, false) && flow.IsManualSaveOpen && !runtime.Playable,
            "게임 정지 상태에서 선택한 빈 칸 저장 후 창 유지");
        check(!await flow.SaveManualAsync(target, false), "저장한 칸을 다시 선택하면 덮어쓰기 승인 필요");
        check(await flow.SaveManualAsync(target, true) && flow.GetSlots(false)[0].Slot == target,
            "승인한 기록만 덮어쓰고 최신순 재정렬");
        store.Pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> writing = flow.SaveManualAsync(target, true);
        check(!flow.CloseManualSave() && !await flow.SaveManualAsync(target, true), "기록 중 닫기와 중복 저장 차단");
        check(flow.CloseManualSave(false) && !flow.IsPlayable, "장면 종료는 기록 중에도 진행을 재개하지 않고 창 수명 종료");
        store.Pending.SetResult(true);
        check(await writing && !runtime.Playable && !flow.IsPlayable, "늦은 기록 성공은 종료된 화면의 게임 진행을 재개하지 않음");
        store.Pending = null;
        check(await flow.ContinueAsync(7) && flow.TryOpenManualSave() && flow.CloseManualSave()
            && flow.IsPlayable && runtime.Playable, "정상 닫기는 게임 진행을 복원");

        var pendingSession = new SaveSession(SaveBuildMode.Demo, 1, store, () => 4000, Array.Empty<SaveRecord>());
        var pendingFlow = new SaveGameFlow(pendingSession, runtime);
        using var commitCancellation = new CancellationTokenSource();
        store.Pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> committing = pendingFlow.NewGameAsync(snapshot, commitCancellation.Token);
        check(pendingFlow.IsBusy && !runtime.Playable && pendingSession.GetRecords().Count == 0,
            "새 게임 초기 파일 기록이 끝나기 전에 진행을 허용하지 않음");
        commitCancellation.Cancel();
        store.Pending.SetResult(true);
        cancelled = false;
        try { await committing; }
        catch (OperationCanceledException) { cancelled = true; }
        check(cancelled && pendingSession.GetRecords().Count == 1 && !runtime.Playable && !pendingFlow.IsPlayable
            && !pendingFlow.IsBusy, "기록 중 취소는 성공한 파일을 유지하고 종료된 장면의 진행만 차단");
        store.Pending = null;

        flow.Ready += state => throw new ApplicationException("준비 완료 소비자 검사 오류");
        failed = false;
        try { await flow.ContinueAsync(7); }
        catch (ApplicationException) { failed = true; }
        check(failed && flow.IsPlayable && runtime.Playable && !flow.IsBusy && session.CurrentSlot == 7,
            "완료 소비자 오류와 복원 실패를 구분하고 성공 상태 유지");
    }

    private sealed class TestRuntime : ISaveGameRuntime
    {
        private readonly SaveSnapshot _snapshot;
        public List<string> Order { get; } = new List<string>();
        public TaskCompletionSource<bool> Pending { get; set; }
        public bool FailPrepare { get; set; }
        public bool FailApply { get; set; }
        public bool Playable { get; private set; }
        public int Captures { get; private set; }
        public int Applies { get; private set; }
        public SaveSnapshot Applied { get; private set; }
        public TestRuntime(SaveSnapshot snapshot) { _snapshot = snapshot; }
        public SaveSnapshot Capture() { Captures++; return _snapshot; }
        public Task PrepareAsync(SaveSnapshot snapshot, CancellationToken cancellation)
        {
            Order.Add("준비");
            if (FailPrepare) throw new InvalidOperationException("맵 준비 검사 실패");
            // 일부러 취소를 무시해 늦은 완료를 재현합니다.
            return Pending?.Task ?? Task.CompletedTask;
        }
        public void Apply(SaveSnapshot snapshot)
        {
            if (FailApply) throw new InvalidOperationException("상태 적용 검사 실패");
            Order.Add("적용");
            Applies++;
            Applied = snapshot;
        }
        public void SetPlayable(bool playable) { Playable = playable; Order.Add(playable ? "진행 허용" : "진행 차단"); }
    }

}
