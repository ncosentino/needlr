```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.202
  [Host]   : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                                        | Mean         | Error       | StdDev      | Ratio    | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
|---------------------------------------------- |-------------:|------------:|------------:|---------:|--------:|-----:|--------:|-------:|----------:|------------:|
| ManualDI_BuildServiceProvider                 |     1.300 μs |   0.0253 μs |   0.0039 μs |     1.00 |    0.00 |    1 |  0.2785 | 0.0916 |   4.56 KB |        1.00 |
| Needlr_Reflection_BuildServiceProvider        | 2,202.034 μs | 580.7134 μs | 150.8094 μs | 1,694.49 |  106.59 |    4 | 54.6875 | 7.8125 | 923.16 KB |      202.34 |
| Needlr_SourceGenExplicit_BuildServiceProvider |    73.950 μs |   3.8803 μs |   1.0077 μs |    56.91 |    0.73 |    2 |  6.3477 | 0.4883 |  109.7 KB |       24.04 |
| Needlr_SourceGenImplicit_BuildServiceProvider |    97.283 μs |   2.0323 μs |   0.3145 μs |    74.86 |    0.30 |    3 | 10.7422 | 0.9766 | 183.41 KB |       40.20 |
