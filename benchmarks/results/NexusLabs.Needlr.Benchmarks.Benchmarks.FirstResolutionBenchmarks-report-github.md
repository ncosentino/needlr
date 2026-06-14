```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

```
| Method                                 | Mean        | Error      | StdDev    | Ratio  | RatioSD | Rank | Allocated | Alloc Ratio |
|--------------------------------------- |------------:|-----------:|----------:|-------:|--------:|-----:|----------:|------------:|
| ManualDI_BuildAndResolveFirst          |    26.98 μs |   7.343 μs |  1.136 μs |   1.00 |    0.05 |    1 |         - |          NA |
| Needlr_Reflection_BuildAndResolveFirst | 5,756.42 μs | 165.370 μs | 25.591 μs | 213.65 |    7.78 |    3 | 1318568 B |          NA |
| Needlr_SourceGen_BuildAndResolveFirst  |   462.51 μs | 117.058 μs | 18.115 μs |  17.17 |    0.87 |    2 |  217096 B |          NA |
