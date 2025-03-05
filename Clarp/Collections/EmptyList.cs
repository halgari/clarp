using System.Collections;
using Clarp.Abstractions;

namespace Clarp.Collections;

public sealed record EmptyList<T> : IImmutableList<T>
{
    public static readonly EmptyList<T> Instance = new();
    
    public T First => throw new InvalidOperationException("Empty list has no first element");
    IImmutableList<T> IImmutableList<T>.Next()
    {
        throw new NotImplementedException();
    }

    public ISeq<T> Next() => throw new InvalidOperationException("Empty list has no next element");

    public int Count => 0;
    public IEnumerator<T> GetEnumerator()
    {
        yield break;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public T this[int index] => throw new IndexOutOfRangeException("Empty list has no elements");
}