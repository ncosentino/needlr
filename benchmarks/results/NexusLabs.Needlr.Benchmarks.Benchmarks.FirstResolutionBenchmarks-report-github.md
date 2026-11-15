```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.404
  [Host]   : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

```
| Method                                 | Mean        | Error      | StdDev     | Ratio  | RatioSD | Rank | Allocated | Alloc Ratio |
|--------------------------------------- |------------:|-----------:|-----------:|-------:|--------:|-----:|----------:|------------:|
| ManualDI_BuildAndResolveFirst          |    31.44 μs |   3.044 μs |   0.471 μs |   1.00 |    0.02 |    1 |         - |          NA |
| Needlr_Reflection_BuildAndResolveFirst | 6,515.51 μs | 694.158 μs | 180.271 μs | 207.30 |    5.96 |    3 | 1320088 B |          NA |
| Needlr_SourceGen_BuildAndResolveFirst  |   502.30 μs | 115.881 μs |  17.933 μs |  15.98 |    0.55 |    2 |  217768 B |          NA |
