using System.Collections.Immutable;
using Clarp.Abstractions;

namespace Clarp.Concurrency;

public abstract class ARef<T> : AReference, IRef<T>
{
    private IRef<T>.ValidatorFn? _validator;

    private IImmutableDictionary<object, IRef<T>.WatchFn>? _watches;

    public abstract T Value { get; }

    public IRef<T>.ValidatorFn? Validator
    {
        get => _validator;
        set
        {
            Validate(value, Value);
            _validator = value;
        }
    }

    public IRef<T> AddWatch(object key, IRef<T>.WatchFn watchFn)
    {
        lock (this)
        {
            _watches ??= ImmutableDictionary<object, IRef<T>.WatchFn>.Empty;
            _watches = _watches.Add(key, watchFn);
            return this;
        }
    }

    public IRef<T> RemoveWatch(object key)
    {
        lock (this)
        {
            if (_watches == null)
                return this;

            _watches = _watches.Remove(key);
            return this;
        }
    }

    public IReadOnlyDictionary<object, IRef<T>.WatchFn> Watches =>
        _watches ?? ImmutableDictionary<object, IRef<T>.WatchFn>.Empty;

    protected void Validate(IRef<T>.ValidatorFn? validator, T value)
    {
        if (validator == null)
            return;

        if (!validator(value))
            throw new ArgumentException($"Value {value} is not valid.");
    }

    protected void Validate(T value)
    {
        Validate(_validator, value);
    }

    public void NotifyWatches(in T oldVal, in T newVal)
    {
        if (_watches == null)
            return;

        var watches = _watches;
        foreach (var (key, watch) in watches) watch.Invoke(key, this, oldVal, newVal);
    }
}