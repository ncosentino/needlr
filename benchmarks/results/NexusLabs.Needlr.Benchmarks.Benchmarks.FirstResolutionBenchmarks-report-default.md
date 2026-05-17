
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]   : .NET 10.0.8 (10.0.826.23019), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.8 (10.0.826.23019), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 Method                                 | Mean        | Error      | StdDev    | Ratio  | RatioSD | Rank | Allocated | Alloc Ratio |
--------------------------------------- |------------:|-----------:|----------:|-------:|--------:|-----:|----------:|------------:|
 ManualDI_BuildAndResolveFirst          |    28.80 μs |   6.220 μs |  0.963 μs |   1.00 |    0.04 |    1 |         - |          NA |
 Needlr_Reflection_BuildAndResolveFirst | 5,723.13 μs | 323.574 μs | 50.073 μs | 198.86 |    6.02 |    3 | 1319568 B |          NA |
 Needlr_SourceGen_BuildAndResolveFirst  |   432.10 μs |  41.613 μs |  6.440 μs |  15.01 |    0.48 |    2 |  217096 B |          NA |
