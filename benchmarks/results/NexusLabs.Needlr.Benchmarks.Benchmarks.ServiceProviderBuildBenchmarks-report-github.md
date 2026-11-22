```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.404
  [Host]   : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  ShortRun : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                                        | Mean         | Error         | StdDev      | Ratio    | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
|---------------------------------------------- |-------------:|--------------:|------------:|---------:|--------:|-----:|--------:|-------:|----------:|------------:|
| ManualDI_BuildServiceProvider                 |     1.043 μs |     0.0385 μs |   0.0060 μs |     1.00 |    0.01 |    1 |  0.2785 | 0.0916 |   4.56 KB |        1.00 |
| Needlr_Reflection_BuildServiceProvider        | 1,871.237 μs | 1,854.1835 μs | 286.9370 μs | 1,794.40 |  246.28 |    4 | 54.6875 | 7.8125 | 923.89 KB |      202.50 |
| Needlr_SourceGenExplicit_BuildServiceProvider |    60.272 μs |     1.2560 μs |   0.3262 μs |    57.80 |    0.41 |    2 |  6.3477 | 0.4883 | 107.04 KB |       23.46 |
| Needlr_SourceGenImplicit_BuildServiceProvider |    83.674 μs |     3.4982 μs |   0.9085 μs |    80.24 |    0.90 |    3 | 11.2305 | 1.4648 | 184.08 KB |       40.35 |
