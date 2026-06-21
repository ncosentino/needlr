
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 Method                                 | Mean        | Error      | StdDev    | Ratio  | RatioSD | Rank | Allocated | Alloc Ratio |
--------------------------------------- |------------:|-----------:|----------:|-------:|--------:|-----:|----------:|------------:|
 ManualDI_BuildAndResolveFirst          |    31.31 μs |   4.397 μs |  0.680 μs |   1.00 |    0.03 |    1 |         - |          NA |
 Needlr_Reflection_BuildAndResolveFirst | 5,848.49 μs | 193.736 μs | 29.981 μs | 186.89 |    3.74 |    3 | 1319576 B |          NA |
 Needlr_SourceGen_BuildAndResolveFirst  |   472.61 μs |  43.305 μs | 11.246 μs |  15.10 |    0.44 |    2 |  217096 B |          NA |
