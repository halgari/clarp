using Clarp.Abstractions;

namespace Clarp.Collections;

public sealed record PersistentList<T> : IPersistentList<T>
{
    private readonly IPersistentList<T> _next;

    internal PersistentList(int count, T first, IPersistentList<T> next)
    {
        Count = count;
        First = first;
        _next = next;
    }
    
    public T First { get; }
    IPersistentList<T> IPersistentList<T>.Next() => _next;

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

            IPersistentList<T> current = this;
            for (var i = 0; i < index; i++)
            {
                current = current.Next();
            }

            return current.First;
        }
    }
}