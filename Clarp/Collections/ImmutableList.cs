using System.Collections;
using Clarp.Abstractions;

namespace Clarp.Collections;

public sealed record ImmutableList<T> : IImmutableList<T>
{
    private readonly IImmutableList<T> _next;

    internal ImmutableList(int count, T first, IImmutableList<T> next)
    {
        Count = count;
        First = first;
        _next = next;
    }
    
    public T First { get; }
    IImmutableList<T> IImmutableList<T>.Next() => _next;

    public ISeq<T> Next() => _next;
    public int Count { get; }
    
    public T this[int index]
    {
        get
        {
            if (index < 0 || index >= Count)
            {
                throw new IndexOutOfRangeException();
            }

            IImmutableList<T> current = this;
            for (var i = 0; i < index; i++)
            {
                current = current.Next();
            }

            return current.First;
        }
    }
}