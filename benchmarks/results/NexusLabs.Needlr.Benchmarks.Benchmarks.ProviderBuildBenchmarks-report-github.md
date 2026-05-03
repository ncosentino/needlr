```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                              | Mean       | Error       | StdDev     | Ratio  | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
|------------------------------------ |-----------:|------------:|-----------:|-------:|--------:|-----:|--------:|-------:|----------:|------------:|
| ManualDI_ToServiceProvider          |   1.233 μs |   0.0751 μs |  0.0195 μs |   1.00 |    0.02 |    1 |  0.2689 | 0.0877 |   4.41 KB |        1.00 |
| Needlr_Reflection_ToServiceProvider | 810.589 μs | 366.3009 μs | 56.6855 μs | 657.54 |   41.99 |    3 | 11.7188 |      - | 252.31 KB |       57.26 |
| Needlr_SourceGen_ToServiceProvider  | 114.384 μs |   4.9699 μs |  0.7691 μs |  92.79 |    1.47 |    2 | 10.7422 | 0.9766 | 183.41 KB |       41.62 |
| Needlr_SourceGen_ToProvider         | 128.901 μs |  63.2631 μs | 16.4292 μs | 104.56 |   12.26 |    2 | 10.7422 | 0.9766 | 188.09 KB |       42.69 |
