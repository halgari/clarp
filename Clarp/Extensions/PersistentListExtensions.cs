using Clarp.Abstractions;
using Clarp.Collections;

namespace Clarp.Extensions;

public static class PersistentListExtensions
{
    public static IPersistentList<T> ToPersistentList<T>(this IReadOnlyList<T> coll)
    {
        IPersistentList<T> list = EmptyList<T>.Instance;
        for (var i = coll.Count - 1; i >= 0; i--)
        {
            list = list.Add(coll[i]);
        }
        return list;
    }
    
}