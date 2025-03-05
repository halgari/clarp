using Clarp.Collections;

namespace Clarp.Abstractions;

/// <summary>
/// A immutable list is much like a ISeq, but the length is finite and known. It is immutable,
/// and is itself a seq
/// </summary>
public interface IImmutableList<T> : ISeq<T>, IReadOnlyList<T>
{
    /// <summary>
    /// Get the count of elements in the list.
    /// </summary>
    public int Count { get; }
    
    /// <summary>
    /// A typed version of Next, which returns a list
    /// </summary>
    public new IImmutableList<T> Next();

    /// <summary>
    /// Returns a new list with the given value added to the start;
    /// </summary>
    public ImmutableList<T> Add(T value)
    {
        return new ImmutableList<T>(Count + 1, value, this);
    }
    
    /// <summary>
    /// Struct enumerator for the list.
    /// </summary>
    public new SeqEnumerator<T> GetEnumerator()
    {
        return new SeqEnumerator<T>(this);
    }
}