using Clarp.Collections;

namespace Clarp.Extensions;

public static class PersistentVectorExtensions
{
    public static PersistentVector<T> ToPersistentVector<T>(this IEnumerable<T> enumerable)
    {
        var vector = PersistentVector<T>.Empty;
        foreach (var item in enumerable)
        {
            vector = vector.Cons(item);
        }
        return vector;
    } 
}