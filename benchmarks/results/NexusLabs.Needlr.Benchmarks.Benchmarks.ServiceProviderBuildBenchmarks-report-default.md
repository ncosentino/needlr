
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                                        | Mean         | Error       | StdDev      | Ratio    | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
---------------------------------------------- |-------------:|------------:|------------:|---------:|--------:|-----:|--------:|-------:|----------:|------------:|
 ManualDI_BuildServiceProvider                 |     1.446 μs |   0.0431 μs |   0.0112 μs |     1.00 |    0.01 |    1 |  0.2785 | 0.0916 |   4.56 KB |        1.00 |
 Needlr_Reflection_BuildServiceProvider        | 2,343.087 μs | 574.3729 μs | 149.1628 μs | 1,620.73 |   94.88 |    4 | 54.6875 | 7.8125 | 923.44 KB |      202.40 |
 Needlr_SourceGenExplicit_BuildServiceProvider |    88.028 μs |   3.0829 μs |   0.4771 μs |    60.89 |    0.52 |    2 |  6.3477 | 0.4883 | 107.04 KB |       23.46 |
 Needlr_SourceGenImplicit_BuildServiceProvider |   119.960 μs |   6.6851 μs |   1.0345 μs |    82.98 |    0.87 |    3 | 11.2305 | 1.4648 | 187.36 KB |       41.07 |
