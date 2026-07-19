
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 Method                                 | Mean        | Error      | StdDev    | Ratio  | RatioSD | Rank | Allocated | Alloc Ratio |
--------------------------------------- |------------:|-----------:|----------:|-------:|--------:|-----:|----------:|------------:|
 ManualDI_BuildAndResolveFirst          |    26.76 μs |   4.687 μs |  0.725 μs |   1.00 |    0.03 |    1 |         - |          NA |
 Needlr_Reflection_BuildAndResolveFirst | 5,486.12 μs | 158.551 μs | 41.175 μs | 205.11 |    5.13 |    3 | 1319576 B |          NA |
 Needlr_SourceGen_BuildAndResolveFirst  |   453.96 μs | 115.685 μs | 30.043 μs |  16.97 |    1.11 |    2 |  216424 B |          NA |
