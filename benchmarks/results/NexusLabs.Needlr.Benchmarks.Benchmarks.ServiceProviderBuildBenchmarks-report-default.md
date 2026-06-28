
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                                        | Mean         | Error       | StdDev     | Ratio    | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
---------------------------------------------- |-------------:|------------:|-----------:|---------:|--------:|-----:|--------:|-------:|----------:|------------:|
 ManualDI_BuildServiceProvider                 |     1.298 μs |   0.0263 μs |  0.0041 μs |     1.00 |    0.00 |    1 |  0.2785 | 0.0916 |   4.56 KB |        1.00 |
 Needlr_Reflection_BuildServiceProvider        | 2,142.248 μs | 110.1584 μs | 17.0471 μs | 1,650.94 |   12.63 |    4 | 54.6875 | 7.8125 | 923.35 KB |      202.38 |
 Needlr_SourceGenExplicit_BuildServiceProvider |    82.435 μs |   1.4484 μs |  0.3761 μs |    63.53 |    0.32 |    2 |  6.4697 | 0.8545 | 106.41 KB |       23.32 |
 Needlr_SourceGenImplicit_BuildServiceProvider |   112.252 μs |   1.3013 μs |  0.2014 μs |    86.51 |    0.28 |    3 | 10.7422 | 0.9766 | 183.42 KB |       40.20 |
