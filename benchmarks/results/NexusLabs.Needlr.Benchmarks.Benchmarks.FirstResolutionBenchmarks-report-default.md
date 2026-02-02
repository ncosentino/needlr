
BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 Method                                 | Mean        | Error      | StdDev    | Ratio  | RatioSD | Rank | Allocated | Alloc Ratio |
--------------------------------------- |------------:|-----------:|----------:|-------:|--------:|-----:|----------:|------------:|
 ManualDI_BuildAndResolveFirst          |    30.30 μs |   2.606 μs |  0.403 μs |   1.00 |    0.02 |    1 |         - |          NA |
 Needlr_Reflection_BuildAndResolveFirst | 5,886.22 μs | 140.363 μs | 36.452 μs | 194.31 |    2.58 |    3 | 1333928 B |          NA |
 Needlr_SourceGen_BuildAndResolveFirst  |   462.42 μs |  75.092 μs | 11.620 μs |  15.27 |    0.39 |    2 |  206848 B |          NA |
