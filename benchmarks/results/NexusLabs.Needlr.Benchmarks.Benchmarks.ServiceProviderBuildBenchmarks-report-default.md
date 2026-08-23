
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                                        | Mean         | Error       | StdDev      | Ratio    | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
---------------------------------------------- |-------------:|------------:|------------:|---------:|--------:|-----:|--------:|-------:|----------:|------------:|
 ManualDI_BuildServiceProvider                 |     1.315 μs |   0.0651 μs |   0.0169 μs |     1.00 |    0.02 |    1 |  0.2785 | 0.0916 |   4.56 KB |        1.00 |
 Needlr_Reflection_BuildServiceProvider        | 2,278.636 μs | 664.9390 μs | 172.6826 μs | 1,733.49 |  121.66 |    4 | 54.6875 | 7.8125 | 923.44 KB |      202.40 |
 Needlr_SourceGenExplicit_BuildServiceProvider |    82.254 μs |   2.2962 μs |   0.5963 μs |    62.58 |    0.85 |    2 |  6.3477 | 0.4883 | 110.32 KB |       24.18 |
 Needlr_SourceGenImplicit_BuildServiceProvider |   115.709 μs |   2.6342 μs |   0.4076 μs |    88.03 |    1.08 |    3 | 11.2305 | 1.4648 | 184.08 KB |       40.35 |
