
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                                        | Mean         | Error      | StdDev     | Ratio    | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
---------------------------------------------- |-------------:|-----------:|-----------:|---------:|--------:|-----:|--------:|-------:|----------:|------------:|
 ManualDI_BuildServiceProvider                 |     1.294 μs |  0.1008 μs |  0.0262 μs |     1.00 |    0.03 |    1 |  0.2785 | 0.0916 |   4.56 KB |        1.00 |
 Needlr_Reflection_BuildServiceProvider        | 2,165.188 μs | 84.2248 μs | 13.0339 μs | 1,673.67 |   32.24 |    4 | 54.6875 | 7.8125 | 923.35 KB |      202.38 |
 Needlr_SourceGenExplicit_BuildServiceProvider |    83.115 μs |  3.3211 μs |  0.8625 μs |    64.25 |    1.33 |    2 |  6.4697 | 0.8545 | 106.41 KB |       23.32 |
 Needlr_SourceGenImplicit_BuildServiceProvider |   110.022 μs |  2.0637 μs |  0.5359 μs |    85.05 |    1.61 |    3 | 10.7422 | 0.9766 | 183.42 KB |       40.20 |
