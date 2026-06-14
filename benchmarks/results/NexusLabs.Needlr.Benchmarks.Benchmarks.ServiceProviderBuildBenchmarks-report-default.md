
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                                        | Mean         | Error      | StdDev     | Ratio    | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
---------------------------------------------- |-------------:|-----------:|-----------:|---------:|--------:|-----:|--------:|-------:|----------:|------------:|
 ManualDI_BuildServiceProvider                 |     1.294 μs |  0.0882 μs |  0.0229 μs |     1.00 |    0.02 |    1 |  0.2785 | 0.0916 |   4.56 KB |        1.00 |
 Needlr_Reflection_BuildServiceProvider        | 2,169.101 μs | 72.6333 μs | 11.2401 μs | 1,677.22 |   28.12 |    4 | 54.6875 | 7.8125 | 923.35 KB |      202.38 |
 Needlr_SourceGenExplicit_BuildServiceProvider |    82.918 μs |  3.3325 μs |  0.5157 μs |    64.12 |    1.09 |    2 |  6.3477 | 0.4883 |  109.7 KB |       24.04 |
 Needlr_SourceGenImplicit_BuildServiceProvider |   121.250 μs | 49.3020 μs | 12.8036 μs |    93.75 |    9.16 |    3 | 10.7422 | 0.9766 | 183.42 KB |       40.20 |
