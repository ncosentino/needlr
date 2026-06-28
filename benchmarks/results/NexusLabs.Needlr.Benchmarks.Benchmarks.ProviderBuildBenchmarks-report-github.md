```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                              | Mean       | Error       | StdDev      | Ratio  | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
|------------------------------------ |-----------:|------------:|------------:|-------:|--------:|-----:|--------:|-------:|----------:|------------:|
| ManualDI_ToServiceProvider          |   1.228 μs |   0.0883 μs |   0.0137 μs |   1.00 |    0.01 |    1 |  0.2689 | 0.0877 |   4.41 KB |        1.00 |
| Needlr_Reflection_ToServiceProvider | 835.806 μs | 387.7489 μs | 100.6972 μs | 680.89 |   75.59 |    3 | 11.7188 |      - | 252.32 KB |       57.26 |
| Needlr_SourceGen_ToServiceProvider  | 115.967 μs |   5.5607 μs |   1.4441 μs |  94.47 |    1.43 |    2 | 10.7422 | 0.9766 |  186.7 KB |       42.37 |
| Needlr_SourceGen_ToProvider         | 124.154 μs |  25.7175 μs |   6.6788 μs | 101.14 |    5.09 |    2 | 10.7422 | 0.9766 | 188.09 KB |       42.69 |
