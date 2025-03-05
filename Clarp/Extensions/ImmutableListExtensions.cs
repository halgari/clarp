using Clarp.Abstractions;
using Clarp.Collections;

namespace Clarp.Extensions;

public static class ImmutableListExtensions
{
    public static IImmutableList<T> ToImmutableList<T>(this IReadOnlyList<T> coll)
    {
        IImmutableList<T> list = EmptyList<T>.Instance;
        for (var i = coll.Count - 1; i >= 0; i--)
        {
            list = list.Add(coll[i]);
        }
        return list;
    }
    
}