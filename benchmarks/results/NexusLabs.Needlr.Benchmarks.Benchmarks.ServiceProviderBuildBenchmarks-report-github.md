```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]   : .NET 10.0.8 (10.0.826.23019), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.8 (10.0.826.23019), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                                        | Mean         | Error       | StdDev     | Ratio    | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
|---------------------------------------------- |-------------:|------------:|-----------:|---------:|--------:|-----:|--------:|-------:|----------:|------------:|
| ManualDI_BuildServiceProvider                 |     1.438 μs |   0.0510 μs |  0.0132 μs |     1.00 |    0.01 |    1 |  0.2785 | 0.0916 |   4.56 KB |        1.00 |
| Needlr_Reflection_BuildServiceProvider        | 2,176.053 μs | 584.2441 μs | 90.4124 μs | 1,513.36 |   57.30 |    4 | 54.6875 | 7.8125 | 923.35 KB |      202.38 |
| Needlr_SourceGenExplicit_BuildServiceProvider |    79.313 μs |   2.1689 μs |  0.5633 μs |    55.16 |    0.58 |    2 |  6.3477 | 0.4883 | 106.42 KB |       23.32 |
| Needlr_SourceGenImplicit_BuildServiceProvider |   108.607 μs |   1.8577 μs |  0.4824 μs |    75.53 |    0.70 |    3 | 11.2305 | 1.4648 |  186.7 KB |       40.92 |
