```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
12th Gen Intel Core i7-12700H, 1 CPU, 20 logical and 10 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

```
| Method                                 | Mean        | Error       | StdDev     | Ratio  | RatioSD | Rank | Allocated  | Alloc Ratio |
|--------------------------------------- |------------:|------------:|-----------:|-------:|--------:|-----:|-----------:|------------:|
| ManualDI_BuildAndResolveFirst          |    29.13 μs |    27.51 μs |   4.258 μs |   1.02 |    0.21 |    1 |    8.17 KB |        1.00 |
| Needlr_Reflection_BuildAndResolveFirst | 4,707.45 μs | 1,375.93 μs | 212.926 μs | 164.67 |   25.88 |    3 | 1291.47 KB |      158.04 |
| Needlr_SourceGen_BuildAndResolveFirst  |   508.97 μs |   492.36 μs | 127.864 μs |  17.80 |    4.94 |    2 |  214.45 KB |       26.24 |
