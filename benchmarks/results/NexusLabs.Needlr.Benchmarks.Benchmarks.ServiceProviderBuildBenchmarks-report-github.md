```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                                        | Mean         | Error       | StdDev     | Ratio    | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
|---------------------------------------------- |-------------:|------------:|-----------:|---------:|--------:|-----:|--------:|-------:|----------:|------------:|
| ManualDI_BuildServiceProvider                 |     1.299 μs |   0.0118 μs |  0.0031 μs |     1.00 |    0.00 |    1 |  0.2785 | 0.0916 |   4.56 KB |        1.00 |
| Needlr_Reflection_BuildServiceProvider        | 2,157.379 μs | 165.3665 μs | 42.9451 μs | 1,660.98 |   30.39 |    4 | 54.6875 | 7.8125 | 923.35 KB |      202.38 |
| Needlr_SourceGenExplicit_BuildServiceProvider |    83.184 μs |   1.9436 μs |  0.5047 μs |    64.04 |    0.38 |    2 |  6.3477 | 0.4883 | 106.42 KB |       23.32 |
| Needlr_SourceGenImplicit_BuildServiceProvider |   111.734 μs |   1.6411 μs |  0.4262 μs |    86.02 |    0.35 |    3 | 10.7422 | 0.9766 | 183.42 KB |       40.20 |
