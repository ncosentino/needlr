```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

```
| Method                                 | Mean        | Error     | StdDev    | Ratio  | RatioSD | Rank | Allocated | Alloc Ratio |
|--------------------------------------- |------------:|----------:|----------:|-------:|--------:|-----:|----------:|------------:|
| ManualDI_BuildAndResolveFirst          |    27.26 μs |  9.661 μs |  1.495 μs |   1.00 |    0.07 |    1 |         - |          NA |
| Needlr_Reflection_BuildAndResolveFirst | 5,827.90 μs | 52.579 μs |  8.137 μs | 214.22 |   10.00 |    3 | 1319568 B |          NA |
| Needlr_SourceGen_BuildAndResolveFirst  |   470.05 μs | 78.479 μs | 12.145 μs |  17.28 |    0.90 |    2 |  217096 B |          NA |
