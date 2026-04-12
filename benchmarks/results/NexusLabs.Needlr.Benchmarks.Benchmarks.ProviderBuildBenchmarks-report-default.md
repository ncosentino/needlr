
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.201
  [Host]   : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                              | Mean       | Error     | StdDev    | Ratio  | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
------------------------------------ |-----------:|----------:|----------:|-------:|--------:|-----:|--------:|-------:|----------:|------------:|
 ManualDI_ToServiceProvider          |   1.122 μs | 0.0162 μs | 0.0042 μs |   1.00 |    0.00 |    1 |  0.2689 | 0.0877 |   4.41 KB |        1.00 |
 Needlr_Reflection_ToServiceProvider | 724.443 μs | 7.4269 μs | 1.1493 μs | 645.68 |    2.40 |    3 | 13.6719 |      - | 252.31 KB |       57.26 |
 Needlr_SourceGen_ToServiceProvider  |  96.045 μs | 0.7065 μs | 0.1835 μs |  85.60 |    0.33 |    2 | 10.7422 | 0.9766 | 183.41 KB |       41.62 |
 Needlr_SourceGen_ToProvider         | 102.843 μs | 4.0339 μs | 1.0476 μs |  91.66 |    0.91 |    2 | 11.2305 | 1.4648 | 188.09 KB |       42.69 |
