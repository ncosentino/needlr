```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                                        | Mean         | Error      | StdDev     | Ratio    | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
|---------------------------------------------- |-------------:|-----------:|-----------:|---------:|--------:|-----:|--------:|-------:|----------:|------------:|
| ManualDI_BuildServiceProvider                 |     1.290 μs |  0.0792 μs |  0.0206 μs |     1.00 |    0.02 |    1 |  0.2785 | 0.0916 |   4.56 KB |        1.00 |
| Needlr_Reflection_BuildServiceProvider        | 2,286.439 μs | 91.2098 μs | 14.1148 μs | 1,772.48 |   27.49 |    4 | 54.6875 | 7.8125 | 936.79 KB |      205.32 |
| Needlr_SourceGenExplicit_BuildServiceProvider |    80.234 μs |  0.8555 μs |  0.2222 μs |    62.20 |    0.91 |    2 |  6.3477 | 0.4883 | 105.84 KB |       23.20 |
| Needlr_SourceGenImplicit_BuildServiceProvider |   107.295 μs |  0.5475 μs |  0.0847 μs |    83.18 |    1.21 |    3 | 10.2539 | 1.4648 | 174.56 KB |       38.26 |
