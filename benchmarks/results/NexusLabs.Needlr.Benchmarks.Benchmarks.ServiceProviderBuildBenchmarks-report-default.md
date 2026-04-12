
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.201
  [Host]   : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                                        | Mean         | Error       | StdDev      | Ratio    | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
---------------------------------------------- |-------------:|------------:|------------:|---------:|--------:|-----:|--------:|-------:|----------:|------------:|
 ManualDI_BuildServiceProvider                 |     1.180 μs |   0.0172 μs |   0.0027 μs |     1.00 |    0.00 |    1 |  0.2785 | 0.0916 |   4.56 KB |        1.00 |
 Needlr_Reflection_BuildServiceProvider        | 2,199.245 μs | 577.1906 μs | 149.8946 μs | 1,863.07 |  116.59 |    4 | 54.6875 | 7.8125 | 923.35 KB |      202.38 |
 Needlr_SourceGenExplicit_BuildServiceProvider |    77.110 μs |   3.8096 μs |   0.9893 μs |    65.32 |    0.78 |    2 |  6.3477 | 0.4883 |  109.7 KB |       24.04 |
 Needlr_SourceGenImplicit_BuildServiceProvider |   101.944 μs |  11.4119 μs |   2.9636 μs |    86.36 |    2.31 |    3 | 11.3525 | 1.8311 |  186.7 KB |       40.92 |
