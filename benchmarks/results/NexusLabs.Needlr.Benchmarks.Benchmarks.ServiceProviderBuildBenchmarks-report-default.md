
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                                        | Mean         | Error       | StdDev      | Ratio    | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
---------------------------------------------- |-------------:|------------:|------------:|---------:|--------:|-----:|--------:|-------:|----------:|------------:|
 ManualDI_BuildServiceProvider                 |     1.274 μs |   0.0920 μs |   0.0239 μs |     1.00 |    0.02 |    1 |  0.2785 | 0.0916 |   4.56 KB |        1.00 |
 Needlr_Reflection_BuildServiceProvider        | 2,230.521 μs | 568.5060 μs | 147.6392 μs | 1,750.81 |  110.00 |    4 | 54.6875 | 7.8125 | 923.17 KB |      202.34 |
 Needlr_SourceGenExplicit_BuildServiceProvider |    77.498 μs |   3.3468 μs |   0.8692 μs |    60.83 |    1.22 |    2 |  6.3477 | 0.4883 | 106.42 KB |       23.32 |
 Needlr_SourceGenImplicit_BuildServiceProvider |   100.548 μs |   5.8424 μs |   0.9041 μs |    78.92 |    1.50 |    3 | 11.2305 | 1.4648 |  186.7 KB |       40.92 |
