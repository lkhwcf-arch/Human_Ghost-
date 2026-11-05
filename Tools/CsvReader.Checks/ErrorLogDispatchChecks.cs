using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HumanGhost.Gameplay.Adapters;

internal static class ErrorLogDispatchChecks
{
    public static void Run(Action<bool> check)
    {
        var context = new TestContext();
        var delivered = new List<ErrorLogMessage>();
        int ownerThread = Environment.CurrentManagedThreadId;
        int callbackThread = 0;
        using var dispatcher = new ErrorLogDispatcher(context, message => { callbackThread = Environment.CurrentManagedThreadId; delivered.Add(message); });
        var time = new DateTimeOffset(2026, 11, 5, 0, 0, 0, TimeSpan.Zero);
        Task.Run(() => { for (int i = 0; i < 100; i++) dispatcher.Post("Error", i.ToString(), "stack", time); }).GetAwaiter().GetResult();
        check(context.Count == 1 && delivered.Count == 0);
        context.Drain();
        check(delivered.Count == 1 && delivered[0].Count == 100 && delivered[0].Message == "99");
        check(callbackThread == ownerThread && delivered[0].Time == time && delivered[0].Stack == "stack");
        Parallel.For(0, 100, i => dispatcher.Post("Exception", i.ToString(), "parallel", time));
        check(context.Count == 1);
        context.Drain();
        check(delivered.Count == 2 && delivered[1].Count == 100);
        dispatcher.Post("Assert", "종료 전", "", time);
        dispatcher.Dispose();
        context.Drain();
        check(delivered.Count == 2);
        dispatcher.Post("Error", "종료 후", "", time);
        check(context.Count == 0);
        using var replacement = new ErrorLogDispatcher(context, delivered.Add);
        replacement.Post("Error", "새 수명", "", time);
        context.Drain();
        check(delivered.Count == 3 && delivered[2].Count == 1);
    }

    private class TestContext : SynchronizationContext
    {
        private readonly Queue<Action> _work = new Queue<Action>();
        public int Count { get { lock (_work) return _work.Count; } }
        public override void Post(SendOrPostCallback callback, object state)
        { lock (_work) _work.Enqueue(() => callback(state)); }
        public void Drain()
        {
            while (true)
            {
                Action action;
                lock (_work) { if (_work.Count == 0) return; action = _work.Dequeue(); }
                action();
            }
        }
    }
}
