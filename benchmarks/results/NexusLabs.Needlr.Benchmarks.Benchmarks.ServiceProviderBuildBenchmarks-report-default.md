
BenchmarkDotNet v0.14.0, Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.404
  [Host]   : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                                        | Mean         | Error       | StdDev      | Ratio    | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
---------------------------------------------- |-------------:|------------:|------------:|---------:|--------:|-----:|--------:|-------:|----------:|------------:|
 ManualDI_BuildServiceProvider                 |     1.435 μs |   0.0919 μs |   0.0239 μs |     1.00 |    0.02 |    1 |  0.2785 | 0.0916 |   4.56 KB |        1.00 |
 Needlr_Reflection_BuildServiceProvider        | 2,336.004 μs | 739.0696 μs | 191.9340 μs | 1,628.03 |  124.60 |    4 | 54.6875 | 7.8125 | 923.89 KB |      202.50 |
 Needlr_SourceGenExplicit_BuildServiceProvider |    86.590 μs |   0.6884 μs |   0.1788 μs |    60.35 |    0.92 |    2 |  6.3477 | 0.4883 | 107.04 KB |       23.46 |
 Needlr_SourceGenImplicit_BuildServiceProvider |   120.225 μs |   2.6195 μs |   0.6803 μs |    83.79 |    1.34 |    3 | 11.2305 | 1.4648 | 184.08 KB |       40.35 |
