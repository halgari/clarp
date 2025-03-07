// See https://aka.ms/new-console-template for more information

using BenchmarkDotNet.Running;
using Clarp.Benchmarks.Collections;

BenchmarkRunner.Run<PersistentVectorBenchmarks>();