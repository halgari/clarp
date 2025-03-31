// See https://aka.ms/new-console-template for more information

using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Running;
using Clarp;
using Clarp.Benchmarks.Collections;
using Clarp.Benchmarks.Concurrency;
using Clarp.Concurrency;
using Clarp.Extensions;
using JetBrains.Profiler.Api;

#if DEBUG

//MemoryProfiler.CollectAllocations(true);
//MemoryProfiler.GetSnapshot();
var r = new Ref<int>(0);
for (int v = 0; v < 100; v++)
{
    for (int i = 0; i < 100; i++)
    {
        Runtime.DoSync(() => r.Value++);
    }
}
//MemoryProfiler.GetSnapshot();
//MemoryProfiler.CollectAllocations(false);


#else 
BenchmarkRunner.Run<STMBenchmarks>();
#endif