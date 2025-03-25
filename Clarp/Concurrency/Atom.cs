using Clarp.Utils;

namespace Clarp.Concurrency;

public class Atom<T> : ARef<T>
{
    private T _value;
    public override T Value => _value;

    /// <summary>
    /// Create a new Atom with the given value
    /// </summary>
    public Atom(T value) => _value = value;

    /// <summary>
    /// Alter the value of the Atom via a function. The function may be called multiple times if the value is changed by another thread,
    /// returns the old and new values of the Atom.
    /// </summary>
    public (T Old, T New) Swap(Func<T, T> func)
    {
        while (true)
        {
            var oldValue = _value;
            var newValue = func(oldValue);
            Validate(newValue);

            if (!Atomic.CompareAndSet(ref _value, oldValue, newValue))
                continue;

            NotifyWatches(oldValue, newValue);
            return (oldValue, newValue);
        }
    }
    
    /// <summary>
    /// Resets the value of the Atom to the given value, returns the old and new values of the Atom. No atomicity guarantees.
    /// </summary>
    public (T Old, T New) Reset(T newValue)
    {
        var oldValue = _value;
        Validate(newValue);
        _value = newValue;
        NotifyWatches(oldValue, newValue);
        return (oldValue, newValue);
    }
}