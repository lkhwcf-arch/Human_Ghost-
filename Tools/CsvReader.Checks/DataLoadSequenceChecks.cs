using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HumanGhost.Data.Infrastructure;

internal static class DataLoadSequenceChecks
{
    public static async Task RunAsync(Action<bool> check)
    {
        var csvReady = new TaskCompletionSource<object>();
        var jsonReady = new TaskCompletionSource<string>();
        var csvDefinitions = new object();
        var steps = new List<string>();
        var sequence = new DataLoadSequence<object, string>(
            token => { steps.Add("CSV 시작"); return csvReady.Task; },
            (csv, token) => { check(ReferenceEquals(csv, csvDefinitions)); steps.Add("JSON 시작"); return jsonReady.Task; });
        Task<string> loading = sequence.LoadAsync(CancellationToken.None);
        check(steps.Count == 1 && !loading.IsCompleted);
        csvReady.SetResult(csvDefinitions);
        check(steps.Count == 2 && !loading.IsCompleted);
        jsonReady.SetResult("완료");
        check(await loading == "완료");

        bool jsonStarted = false;
        var failure = new InvalidOperationException("CSV 실패");
        var failed = new DataLoadSequence<object, string>(token => Task.FromException<object>(failure),
            (csv, token) => { jsonStarted = true; return Task.FromResult(""); });
        try { await failed.LoadAsync(CancellationToken.None); check(false); }
        catch (InvalidOperationException error) { check(ReferenceEquals(error, failure) && !jsonStarted); }

        using var cancellation = new CancellationTokenSource();
        var delayedCsv = new TaskCompletionSource<object>();
        var cancelled = new DataLoadSequence<object, string>(token => delayedCsv.Task,
            (csv, token) => { jsonStarted = true; return Task.FromResult(""); });
        Task<string> pending = cancelled.LoadAsync(cancellation.Token);
        cancellation.Cancel(); delayedCsv.SetResult(csvDefinitions);
        try { await pending; check(false); }
        catch (OperationCanceledException) { check(!jsonStarted); }
        bool csvStarted = false;
        var beforeStart = new DataLoadSequence<object, string>(token => { csvStarted = true; return Task.FromResult(csvDefinitions); },
            (csv, token) => { jsonStarted = true; return Task.FromResult(""); });
        try { await beforeStart.LoadAsync(cancellation.Token); check(false); }
        catch (OperationCanceledException) { check(!csvStarted && !jsonStarted); }
    }
}
