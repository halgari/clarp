﻿using BenchmarkDotNet.Attributes;
using Clarp.Abstractions;
using Clarp.Collections;

namespace Clarp.Benchmarks.Collections;

[MemoryDiagnoser]
public class ImmutableListBenchmarks
{
    private List<int> _mutableList;
    private System.Collections.Immutable.ImmutableList<int> _msImmutableList;
    private IImmutableList<int> _clarpImmutableList;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _mutableList = [];
        _msImmutableList = System.Collections.Immutable.ImmutableList<int>.Empty;
        _clarpImmutableList = EmptyList<int>.Instance;
        for (var i = 0; i < Size; i++)
        {
            _mutableList.Add(i);
            _msImmutableList = _msImmutableList.Add(i);
            _clarpImmutableList = _clarpImmutableList.Add(i);
        }
        
    }
    
    [Params(1, 100, 1000, 10000)]
    public int Size { get; set; }
    
    [Benchmark]
    public long IterationMutableList()
    {
        long sum = 0;
        foreach (var i in _mutableList)
        {
            sum += i;
        }
        return sum;
    }
    
    [Benchmark]
    public long IterationMSImmutableList()
    {
        long sum = 0;
        foreach (var i in _msImmutableList)
        {
            sum += i;
        }
        return sum;
    }
    
    [Benchmark]
    public long IterationClarpImmutableList()
    {
        long sum = 0;
        foreach (var i in _clarpImmutableList)
        {
            sum += i;
        }
        return sum;
    }
}