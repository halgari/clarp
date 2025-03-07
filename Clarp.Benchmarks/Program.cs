// See https://aka.ms/new-console-template for more information

using BenchmarkDotNet.Running;
using Clarp.Benchmarks.Collections;
using Clarp.Extensions;

#if DEBUG
var vector = Enumerable.Range(0, 1000).ToPersistentVector();

var sum = 0;
for (int v = 0; v < 100; v++)
{
    for (int i = 0; i < 1000; i++)
    {
        sum += vector[i];
    }
}


#else 
BenchmarkRunner.Run<PersistentVectorBenchmarks>();
#endif