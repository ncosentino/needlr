
BenchmarkDotNet v0.14.0, Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.404
  [Host]   : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  ShortRun : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 Method                                 | Mean        | Error      | StdDev    | Ratio  | RatioSD | Rank | Allocated | Alloc Ratio |
--------------------------------------- |------------:|-----------:|----------:|-------:|--------:|-----:|----------:|------------:|
 ManualDI_BuildAndResolveFirst          |    20.76 μs |   6.219 μs |  0.962 μs |   1.00 |    0.06 |    1 |         - |          NA |
 Needlr_Reflection_BuildAndResolveFirst | 4,205.66 μs | 124.657 μs | 19.291 μs | 202.92 |    8.40 |    3 | 1320088 B |          NA |
 Needlr_SourceGen_BuildAndResolveFirst  |   333.76 μs |  55.464 μs | 14.404 μs |  16.10 |    0.92 |    2 |  217768 B |          NA |
