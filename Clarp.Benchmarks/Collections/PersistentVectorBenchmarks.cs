using BenchmarkDotNet.Attributes;
using Clarp.Abstractions;
using Clarp.Collections;

namespace Clarp.Benchmarks.Collections;

[MemoryDiagnoser]
public class PersistentVectorBenchmarks
{
    private List<int> _mutableList;
    private System.Collections.Immutable.ImmutableList<int> _msImmutableList;
    private PersistentVector<int> _clarpPersistentVector;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _mutableList = [];
        _msImmutableList = System.Collections.Immutable.ImmutableList<int>.Empty;
        _clarpPersistentVector = PersistentVector<int>.Empty;
        for (var i = 0; i < Size; i++)
        {
            _mutableList.Add(i);
            _msImmutableList = _msImmutableList.Add(i);
            _clarpPersistentVector = _clarpPersistentVector.Cons(i);
        }
        
    }
    
    //[Params(1, 100, 1000, 10000)]
    [Params(100)]
    public int Size { get; set; }
    
    
    [Benchmark]
    public long GetMidItemMutableList()
    {
        return _mutableList[Size / 2];
    }

    [Benchmark]
    public long GetMidItemMSImmutableList()
    {
        return _msImmutableList[Size / 2];
    }

    [Benchmark]
    public long IterationClarpImmutableList()
    {
        return _clarpPersistentVector[Size / 2];
    }

    
    [Benchmark]
    public System.Collections.Immutable.ImmutableList<int> AddItemMSImmutableList()
    {
        return _msImmutableList.Add(Size);
    }
    
    [Benchmark]
    public PersistentVector<int> AddItemClarpImmutableList()
    {
        return _clarpPersistentVector.Cons(Size);
    }
    
}