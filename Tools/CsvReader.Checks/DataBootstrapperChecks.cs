using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HumanGhost.Data.Csv;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Infrastructure;
using HumanGhost.Data.Mapping;
using HumanGhost.Data.Storage;

internal static class DataBootstrapperChecks
{
    public static async Task RunAsync(Action<bool> check)
    {
        var files = new List<string>();
        var source = new Source(async (name, token) =>
        {
            files.Add(name);
            return await new Utf8CsvFileSource().ReadTextAsync(name, token);
        });
        string ResolvePath(string relative) => Path.Combine(AppContext.BaseDirectory, Path.GetFileName(relative));
        var bootstrapper = new DataBootstrapper(source, ResolvePath);
        int notifications = 0;
        IReadOnlyDataCatalog notified = null;
        bootstrapper.Initialized += data =>
        {
            notifications++;
            notified = data;
            check(ReferenceEquals(bootstrapper.Data, data));
            check(ReferenceEquals(bootstrapper.InitializeAsync(CancellationToken.None), bootstrapper.InitializeAsync(CancellationToken.None)));
        };
        Task<IReadOnlyDataCatalog> first = bootstrapper.InitializeAsync(CancellationToken.None);
        check(ReferenceEquals(first, bootstrapper.InitializeAsync(CancellationToken.None)));
        IReadOnlyDataCatalog result = await first;
        check(notifications == 1 && ReferenceEquals(result, notified) && ReferenceEquals(result, bootstrapper.Data));
        check(files.Count == 13 && Path.GetFileName(files[0]) == "UnitStat.csv");
        check(files.Take(9).All(path => path.EndsWith(".csv")) && files.Skip(9).All(path => path.EndsWith(".json")));
        check(result.GetTable<Player>().All.All(player => player.HP == result.GetTable<UnitStat>().Get(player.UnitID).HP));
        check(result.GetTable<Boss>().All.All(boss => boss.States.All(state => state.UnitID == boss.ID)));
        check(result.GetTable<UnitStateGroup<PlayerState>>().Count == 1);
        check(result.GetTable<Elite>().Count == 0 && result.GetTable<UnitStateGroup<EliteState>>().Count == 0);
        await bootstrapper.InitializeAsync(CancellationToken.None);
        check(files.Count == 13 && notifications == 1);

        var failure = new IOException("로딩 실패 검사");
        var failedFiles = new List<string>();
        var failed = new DataBootstrapper(new Source((name, token) =>
        {
            failedFiles.Add(name);
            return Task.FromException<string>(failure);
        }), ResolvePath);
        int failedNotifications = 0;
        failed.Initialized += data => failedNotifications++;
        try { await failed.InitializeAsync(CancellationToken.None); check(false); }
        catch (IOException error) { check(ReferenceEquals(error, failure)); }
        check(failedFiles.Count == 1 && failed.Data == null && failedNotifications == 0);

        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var cancelled = new DataBootstrapper(source, ResolvePath);
        try { await cancelled.InitializeAsync(cancellation.Token); check(false); }
        catch (OperationCanceledException) { check(cancelled.Data == null && files.Count == 13); }

        var jsonFailed = new DataBootstrapper(new Source((name, token) => name.EndsWith(".json")
            ? Task.FromException<string>(failure) : new Utf8CsvFileSource().ReadTextAsync(name, token)), ResolvePath);
        jsonFailed.Initialized += data => failedNotifications++;
        try { await jsonFailed.InitializeAsync(CancellationToken.None); check(false); }
        catch (IOException) { check(jsonFailed.Data == null && failedNotifications == 0); }
    }

    private sealed class Source : ICsvTextSource
    {
        private readonly Func<string, CancellationToken, Task<string>> _read;
        public Source(Func<string, CancellationToken, Task<string>> read) { _read = read; }
        public Task<string> ReadTextAsync(string sourceName, CancellationToken token) => _read(sourceName, token);
    }
}
