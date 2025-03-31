using BenchmarkDotNet.Attributes;
using Clarp.Concurrency;

namespace Clarp.Benchmarks.Concurrency;

[MemoryDiagnoser]
public class STMBenchmarks
{
    private Ref<int> _ref;

    [GlobalSetup]
    public void Setup()
    {
        _ref = new Ref<int>(0);
    }

    [Benchmark]
    public int SingleTransaction()
    {
        return Runtime.DoSync(() => _ref.Value++);
    }
    
    [Benchmark]
    public int TwoNestedTransaction()
    {
        return Runtime.DoSync(() =>
        {
            var result = _ref.Value;
            return Runtime.DoSync(() => _ref.Value++);
        });
    }
    
    [Benchmark]
    public int ThreeNestedTransaction()
    {
        return Runtime.DoSync(() =>
        {
            var result = _ref.Value;
            return Runtime.DoSync(() =>
            {
                var result2 = _ref.Value;
                return Runtime.DoSync(() => _ref.Value++);
            });
        });
    }
}