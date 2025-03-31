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
        return Runtime.DoSync(static r =>
        {
            return Runtime.DoSync(static r => r.Value++, r);
        }, _ref);
    }
    
    [Benchmark]
    public int ThreeNestedTransaction()
    {
        return Runtime.DoSync(static r =>
        {
            return Runtime.DoSync(static r =>
            {
                return Runtime.DoSync(static r => r.Value++, r);
            }, r);
        }, _ref);
    }
}