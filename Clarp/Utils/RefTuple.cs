namespace Clarp.Utils;

public static class RefTuple
{
    public static RefTuple<T1, T2> Create<T1, T2>(T1 item1, T2 item2) 
        where T1 : allows ref struct
        where T2 : allows ref struct
    {
        return new RefTuple<T1, T2>
        {
            Item1 = item1,
            Item2 = item2
        };
    }
}

/// <summary>
/// A tuple-like ref-struct 
/// </summary>
public readonly ref struct RefTuple<T1, T2>
    where T1 : allows ref struct
    where T2 : allows ref struct
{
    public required T1 Item1 { get; init; }
    public required T2 Item2 { get; init; }
    
    public void Deconstruct(out T1 item1, out T2 item2)
    {
        item1 = Item1;
        item2 = Item2;
    }
}