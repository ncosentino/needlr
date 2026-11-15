```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.404
  [Host]   : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                              | Mean       | Error      | StdDev     | Ratio  | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
|------------------------------------ |-----------:|-----------:|-----------:|-------:|--------:|-----:|--------:|-------:|----------:|------------:|
| ManualDI_ToServiceProvider          |   1.344 μs |  0.0391 μs |  0.0101 μs |   1.00 |    0.01 |    1 |  0.2689 | 0.0877 |   4.41 KB |        1.00 |
| Needlr_Reflection_ToServiceProvider | 786.655 μs | 14.8710 μs |  3.8619 μs | 585.48 |    4.82 |    3 | 13.6719 |      - | 252.82 KB |       57.38 |
| Needlr_SourceGen_ToServiceProvider  | 119.481 μs |  2.3332 μs |  0.6059 μs |  88.93 |    0.74 |    2 | 10.7422 | 0.9766 | 184.08 KB |       41.78 |
| Needlr_SourceGen_ToProvider         | 134.041 μs | 62.9798 μs | 16.3557 μs |  99.76 |   11.13 |    2 | 10.7422 | 0.9766 | 188.75 KB |       42.84 |
