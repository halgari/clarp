using BenchmarkDotNet.Attributes;
using Clarp.Collections;

namespace Clarp.Benchmarks.Collections;

[MemoryDiagnoser]
public class PersistentVectorBenchmarks
{
    private List<int> _mutableList;
    private System.Collections.Immutable.ImmutableList<int> _immutableList;
    private PersistentVector<int> _clarpPersistentVector;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _mutableList = [];
        _immutableList = System.Collections.Immutable.ImmutableList<int>.Empty;
        _clarpPersistentVector = PersistentVector<int>.Empty;
        for (var i = 0; i < Size; i++)
        {
            _mutableList.Add(i);
            _immutableList = _immutableList.Add(i);
            _clarpPersistentVector = _clarpPersistentVector.Cons(i);
        }
        
    }
    
    [Params(1, 100, 32 * 32, 32 * 32 * 32)]
    //[Params(100)]
    public int Size { get; set; }
    
    
    [Benchmark]
    public long GetMidItemMutableList()
    {
        return _mutableList[Size / 2];
    }

    [Benchmark]
    public long GetMidItemImmutableList()
    {
        return _immutableList[Size / 2];
    }

    [Benchmark]
    public long IterationClarpPersistentVector()
    {
        return _clarpPersistentVector[Size / 2];
    }

    
    [Benchmark]
    public System.Collections.Immutable.ImmutableList<int> AddItemImmutableList()
    {
        return _immutableList.Add(Size);
    }
    
    [Benchmark]
    public PersistentVector<int> AddItemClarpPersistentVector()
    {
        return _clarpPersistentVector.Cons(Size);
    }
    
    [Benchmark]
    public List<int> BuildMutableList()
    {
        var list = new List<int>();
        for (var i = 0; i < Size; i++)
        {
            list.Add(i);
        }

        return list;
    }
    
    [Benchmark]
    public System.Collections.Immutable.ImmutableList<int> BuildImmutableList()
    {
        var list = System.Collections.Immutable.ImmutableList<int>.Empty;
        for (var i = 0; i < Size; i++)
        {
            list = list.Add(i);
        }
        return list;
    }
    
    [Benchmark]
    public PersistentVector<int> BuildClarpPersistentVector()
    {
        var vector = PersistentVector<int>.Empty;
        for (var i = 0; i < Size; i++)
        {
            vector = vector.Cons(i);
        }
        return vector;
    }
    
}