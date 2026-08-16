```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
Intel Core 7 150U, 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

```
| Method                                 | Mean        | Error     | StdDev    | Ratio  | RatioSD | Rank | Allocated | Alloc Ratio |
|--------------------------------------- |------------:|----------:|----------:|-------:|--------:|-----:|----------:|------------:|
| ManualDI_BuildAndResolveFirst          |    24.87 μs |  21.46 μs |  3.321 μs |   1.01 |    0.17 |    1 |         - |          NA |
| Needlr_Reflection_BuildAndResolveFirst | 4,039.51 μs | 313.13 μs | 81.319 μs | 164.61 |   19.58 |    3 | 1321312 B |          NA |
| Needlr_SourceGen_BuildAndResolveFirst  |   332.40 μs |  81.83 μs | 12.663 μs |  13.55 |    1.67 |    2 |  219176 B |          NA |
