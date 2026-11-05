using System;
using System.Threading;
using System.Threading.Tasks;
using HumanGhost.Gameplay.Adapters;

internal static class SoundPreparationChecks
{
    private class Source : IAssetSource<string>
    {
        public int Loads;
        public int Releases;
        public TaskCompletionSource<AssetLease<string>> Pending = new TaskCompletionSource<AssetLease<string>>();
        public Task<AssetLease<string>> LoadAsync(string address, CancellationToken cancellation)
        {
            Loads++;
            return Pending.Task;
        }
        public void Complete() => Pending.SetResult(new AssetLease<string>("음원", () => Releases++));
    }

    private static async Task ExpectCanceled(Task task, Action<bool, string> check, string message)
    {
        try { await task; }
        catch (OperationCanceledException) { check(true, message); return; }
        check(false, message);
    }

    public static async Task RunAsync(Action<bool, string> check)
    {
        var source = new Source();
        var store = new PreparedAssetStore<string>(source);
        Task first = store.PrepareAsync(new[] { "sound/a", "sound/a" });
        Task second = store.PrepareAsync(new[] { "sound/a" });
        check(source.Loads == 1, "같은 주소의 중복 준비는 한 번만 로드");
        source.Complete();
        await Task.WhenAll(first, second);
        check(store.TryGet("sound/a", out string asset) && asset == "음원", "준비 완료 리소스 조회");
        store.Dispose(); store.Dispose();
        check(source.Releases == 1 && !store.TryGet("sound/a", out _), "반복 종료는 한 번만 반환하고 조회 차단");

        source = new Source();
        store = new PreparedAssetStore<string>(source);
        Task late = store.PrepareAsync(new[] { "sound/a" });
        store.Dispose();
        source.Complete();
        await ExpectCanceled(late, check, "종료 뒤 늦은 준비 완료는 취소");
        check(source.Releases == 1, "종료 뒤 도착한 리소스도 한 번 반환");

        source = new Source();
        store = new PreparedAssetStore<string>(source);
        late = store.PrepareAsync(new[] { "sound/a" });
        store.Dispose();
        source.Pending.SetException(new InvalidOperationException("늦은 공급 실패"));
        await ExpectCanceled(late, check, "종료 뒤 공급 실패는 이전 수명의 취소로 처리");

        source = new Source();
        using (store = new PreparedAssetStore<string>(source))
        {
            Task failed = store.PrepareAsync(new[] { "sound/a" });
            source.Pending.SetException(new InvalidOperationException("공급 실패"));
            try { await failed; check(false, "준비 실패 전달"); }
            catch (InvalidOperationException) { check(true, "준비 실패 전달"); }
            source.Pending = new TaskCompletionSource<AssetLease<string>>();
            Task retry = store.PrepareAsync(new[] { "sound/a" });
            source.Complete(); await retry;
            check(source.Loads == 2 && store.TryGet("sound/a", out _), "실패한 주소 재시도 가능");
        }

        source = new Source();
        using (store = new PreparedAssetStore<string>(source))
        using (var cancellation = new CancellationTokenSource())
        {
            Task canceled = store.PrepareAsync(new[] { "sound/a", "sound/b" }, cancellation.Token);
            cancellation.Cancel(); source.Complete();
            await ExpectCanceled(canceled, check, "호출 취소 후 다음 주소 준비 중단");
            check(source.Loads == 1 && store.TryGet("sound/a", out _), "공유 준비 리소스는 저장소 수명까지 유지");
        }
        check(source.Releases == 1, "취소된 호출의 공유 리소스도 종료 시 반환");
    }
}
