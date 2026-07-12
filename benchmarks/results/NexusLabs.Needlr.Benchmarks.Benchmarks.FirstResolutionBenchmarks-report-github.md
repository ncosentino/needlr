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
| ManualDI_BuildAndResolveFirst          |    27.32 μs |   7.276 μs |  1.126 μs |   1.00 |    0.05 |    1 |         - |          NA |
| Needlr_Reflection_BuildAndResolveFirst | 5,807.15 μs | 129.616 μs | 20.058 μs | 212.84 |    7.70 |    3 | 1319576 B |          NA |
| Needlr_SourceGen_BuildAndResolveFirst  |   477.67 μs | 146.495 μs | 38.044 μs |  17.51 |    1.43 |    2 |  217096 B |          NA |
