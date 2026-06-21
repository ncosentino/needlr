```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                                        | Mean         | Error       | StdDev     | Ratio    | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
|---------------------------------------------- |-------------:|------------:|-----------:|---------:|--------:|-----:|--------:|-------:|----------:|------------:|
| ManualDI_BuildServiceProvider                 |     1.314 μs |   0.1026 μs |  0.0266 μs |     1.00 |    0.03 |    1 |  0.2785 | 0.0916 |   4.56 KB |        1.00 |
| Needlr_Reflection_BuildServiceProvider        | 2,220.600 μs | 296.1192 μs | 76.9012 μs | 1,690.84 |   62.17 |    4 | 54.6875 | 7.8125 | 923.35 KB |      202.38 |
| Needlr_SourceGenExplicit_BuildServiceProvider |    85.379 μs |   1.4511 μs |  0.3768 μs |    65.01 |    1.25 |    2 |  6.3477 | 0.4883 |  109.7 KB |       24.04 |
| Needlr_SourceGenImplicit_BuildServiceProvider |   114.091 μs |   5.6318 μs |  0.8715 μs |    86.87 |    1.74 |    3 | 10.7422 | 0.9766 | 183.42 KB |       40.20 |
