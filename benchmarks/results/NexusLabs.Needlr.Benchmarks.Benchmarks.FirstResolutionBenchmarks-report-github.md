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
| ManualDI_BuildAndResolveFirst          |    30.42 μs |   2.843 μs |  0.440 μs |   1.00 |    0.02 |    1 |         - |          NA |
| Needlr_Reflection_BuildAndResolveFirst | 5,953.18 μs | 197.470 μs | 51.282 μs | 195.70 |    2.93 |    3 | 1319576 B |          NA |
| Needlr_SourceGen_BuildAndResolveFirst  |   478.38 μs |  37.215 μs |  9.664 μs |  15.73 |    0.35 |    2 |  217096 B |          NA |
