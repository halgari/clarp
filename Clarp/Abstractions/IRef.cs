using System.Collections.Immutable;

namespace Clarp.Abstractions;

public interface IRef<T> : IDeref<T>
{
    public delegate bool ValidatorFn(in T value);
    
    public delegate void WatchFn(object key, IRef<T> reference, in T oldValue, in T newValue);
    
    /// <summary>
    /// Get or set the validator for the reference.
    /// </summary>
    public ValidatorFn? Validator { get; set; }
    
    /// <summary>
    /// Add the watch function to the reference, function will be called when the reference is updated, and will be passed
    /// the key, the reference, the old value, and the new value.
    /// </summary>
    public IRef<T> AddWatch(object key, WatchFn watchFn);
    
    /// <summary>
    /// Remove the watch function with the associated key.
    /// </summary>
    public IRef<T> RemoveWatch(object key);
    
    /// <summary>
    /// Get the watches associated with the reference.
    /// </summary>
    public IReadOnlyDictionary<object, WatchFn> Watches { get; }
}