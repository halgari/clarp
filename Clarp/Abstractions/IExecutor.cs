namespace Clarp.Abstractions;

public interface IExecutor
{
    public void Execute<T>(Action<T> action, T state);
}