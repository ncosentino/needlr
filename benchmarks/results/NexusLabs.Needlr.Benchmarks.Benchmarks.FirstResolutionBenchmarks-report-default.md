
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 Method                                 | Mean        | Error      | StdDev    | Ratio  | RatioSD | Rank | Allocated | Alloc Ratio |
--------------------------------------- |------------:|-----------:|----------:|-------:|--------:|-----:|----------:|------------:|
 ManualDI_BuildAndResolveFirst          |    31.53 μs |   8.475 μs |  1.312 μs |   1.00 |    0.05 |    1 |         - |          NA |
 Needlr_Reflection_BuildAndResolveFirst | 6,159.44 μs | 282.975 μs | 73.488 μs | 195.58 |    7.62 |    3 | 1319568 B |          NA |
 Needlr_SourceGen_BuildAndResolveFirst  |   472.02 μs |  94.533 μs | 24.550 μs |  14.99 |    0.91 |    2 |  217096 B |          NA |
