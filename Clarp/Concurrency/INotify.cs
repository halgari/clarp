namespace Clarp.Concurrency;

public interface INotify
{
    void Notify(object? oldValue, object? newValue);
}