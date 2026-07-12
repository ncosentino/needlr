
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                                        | Mean         | Error      | StdDev    | Ratio    | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
---------------------------------------------- |-------------:|-----------:|----------:|---------:|--------:|-----:|--------:|-------:|----------:|------------:|
 ManualDI_BuildServiceProvider                 |     1.391 μs |  0.1074 μs | 0.0279 μs |     1.00 |    0.03 |    1 |  0.2785 | 0.0916 |   4.56 KB |        1.00 |
 Needlr_Reflection_BuildServiceProvider        | 2,287.449 μs | 41.8966 μs | 6.4835 μs | 1,645.05 |   30.44 |    4 | 54.6875 | 7.8125 | 923.19 KB |      202.34 |
 Needlr_SourceGenExplicit_BuildServiceProvider |    85.815 μs |  2.4662 μs | 0.6405 μs |    61.71 |    1.20 |    2 |  6.3477 | 0.4883 |  109.7 KB |       24.04 |
 Needlr_SourceGenImplicit_BuildServiceProvider |   115.840 μs |  3.9331 μs | 1.0214 μs |    83.31 |    1.66 |    3 | 10.7422 | 0.9766 | 183.41 KB |       40.20 |
