namespace Clarp.Utils;

public class AtomicReference<T> where T : class
{
    public T Value { get; }

    public AtomicReference(T value)
    {
        Value = value;
    }
    
}