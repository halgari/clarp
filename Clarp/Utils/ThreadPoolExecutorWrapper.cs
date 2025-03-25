using Clarp.Abstractions;

namespace Clarp.Utils;

public sealed class ThreadPoolExecutorWrapper : IExecutor
{
    public static readonly ThreadPoolExecutorWrapper Instance = new();
    public void Execute<T>(Action<T> action, T state)
    {
        ThreadPool.QueueUserWorkItem(static s =>
        {
            var (a, t) = ((Action<T>, T))s!;
            a(t);
        }, (action, state));
    }
}