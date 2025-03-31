using Clarp.Abstractions;

namespace Clarp.Utils;

public static class Watcher
{
    public delegate void WatcherCallback<in T>(object key, IReference reference, T? oldValue, T? newValue);
}