
BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                                        | Mean         | Error      | StdDev     | Ratio    | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
---------------------------------------------- |-------------:|-----------:|-----------:|---------:|--------:|-----:|--------:|-------:|----------:|------------:|
 ManualDI_BuildServiceProvider                 |     1.318 μs |  0.0645 μs |  0.0167 μs |     1.00 |    0.02 |    1 |  0.2785 | 0.0916 |   4.56 KB |        1.00 |
 Needlr_Reflection_BuildServiceProvider        | 2,248.771 μs | 70.3427 μs | 10.8856 μs | 1,706.82 |   21.15 |    4 | 54.6875 | 7.8125 | 936.79 KB |      205.32 |
 Needlr_SourceGenExplicit_BuildServiceProvider |    82.291 μs |  1.4864 μs |  0.2300 μs |    62.46 |    0.74 |    2 |  6.2256 | 0.8545 | 102.94 KB |       22.56 |
 Needlr_SourceGenImplicit_BuildServiceProvider |   106.265 μs |  3.5220 μs |  0.9147 μs |    80.66 |    1.13 |    3 | 10.2539 | 1.4648 | 174.56 KB |       38.26 |
