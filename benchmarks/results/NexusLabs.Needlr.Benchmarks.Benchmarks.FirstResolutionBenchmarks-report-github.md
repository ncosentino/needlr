```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

```
| Method                                 | Mean        | Error     | StdDev    | Ratio  | RatioSD | Rank | Allocated | Alloc Ratio |
|--------------------------------------- |------------:|----------:|----------:|-------:|--------:|-----:|----------:|------------:|
| ManualDI_BuildAndResolveFirst          |    40.87 μs |  45.99 μs |  11.94 μs |   1.06 |    0.38 |    1 |         - |          NA |
| Needlr_Reflection_BuildAndResolveFirst | 5,492.24 μs | 566.07 μs | 147.01 μs | 142.73 |   32.98 |    3 | 1319568 B |          NA |
| Needlr_SourceGen_BuildAndResolveFirst  |   434.99 μs |  54.75 μs |  14.22 μs |  11.30 |    2.62 |    2 |  217096 B |          NA |
