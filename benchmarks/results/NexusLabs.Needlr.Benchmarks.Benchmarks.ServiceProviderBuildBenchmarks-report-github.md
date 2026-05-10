```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                                        | Mean         | Error       | StdDev     | Ratio    | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
|---------------------------------------------- |-------------:|------------:|-----------:|---------:|--------:|-----:|--------:|-------:|----------:|------------:|
| ManualDI_BuildServiceProvider                 |     1.347 μs |   0.1261 μs |  0.0327 μs |     1.00 |    0.03 |    1 |  0.2785 | 0.0916 |   4.56 KB |        1.00 |
| Needlr_Reflection_BuildServiceProvider        | 2,077.223 μs | 153.1315 μs | 23.6973 μs | 1,542.44 |   38.34 |    4 | 54.6875 | 7.8125 | 923.16 KB |      202.34 |
| Needlr_SourceGenExplicit_BuildServiceProvider |    77.643 μs |   3.6049 μs |  0.9362 μs |    57.65 |    1.45 |    2 |  6.3477 | 0.4883 | 106.42 KB |       23.32 |
| Needlr_SourceGenImplicit_BuildServiceProvider |   100.993 μs |   6.6457 μs |  1.0284 μs |    74.99 |    1.83 |    3 | 10.7422 | 0.9766 | 183.41 KB |       40.20 |
