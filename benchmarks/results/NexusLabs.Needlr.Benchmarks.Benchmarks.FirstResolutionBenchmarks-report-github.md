```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

```
| Method                                 | Mean        | Error     | StdDev    | Ratio  | RatioSD | Rank | Allocated | Alloc Ratio |
|--------------------------------------- |------------:|----------:|----------:|-------:|--------:|-----:|----------:|------------:|
| ManualDI_BuildAndResolveFirst          |    26.53 μs |  11.55 μs |  1.787 μs |   1.00 |    0.08 |    1 |         - |          NA |
| Needlr_Reflection_BuildAndResolveFirst | 6,128.07 μs | 196.99 μs | 51.158 μs | 231.70 |   13.13 |    3 | 1319664 B |          NA |
| Needlr_SourceGen_BuildAndResolveFirst  |   491.34 μs |  86.21 μs | 22.390 μs |  18.58 |    1.30 |    2 |  217480 B |          NA |
