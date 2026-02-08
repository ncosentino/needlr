
BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 Method                                 | Mean        | Error    | StdDev    | Median      | Ratio  | RatioSD | Rank | Allocated | Alloc Ratio |
--------------------------------------- |------------:|---------:|----------:|------------:|-------:|--------:|-----:|----------:|------------:|
 ManualDI_BuildAndResolveFirst          |    33.43 μs | 40.28 μs | 10.461 μs |    26.15 μs |   1.07 |    0.42 |    1 |         - |          NA |
 Needlr_Reflection_BuildAndResolveFirst | 5,901.96 μs | 11.98 μs |  1.854 μs | 5,901.88 μs | 189.79 |   48.61 |    3 | 1333928 B |          NA |
 Needlr_SourceGen_BuildAndResolveFirst  |   468.99 μs | 88.84 μs | 23.071 μs |   470.80 μs |  15.08 |    3.91 |    2 |  206848 B |          NA |
