
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                              | Mean       | Error       | StdDev      | Ratio  | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
------------------------------------ |-----------:|------------:|------------:|-------:|--------:|-----:|--------:|-------:|----------:|------------:|
 ManualDI_ToServiceProvider          |   1.227 μs |   0.0454 μs |   0.0118 μs |   1.00 |    0.01 |    1 |  0.2689 | 0.0877 |   4.41 KB |        1.00 |
 Needlr_Reflection_ToServiceProvider | 848.231 μs | 436.9116 μs | 113.4646 μs | 691.26 |   84.63 |    3 | 11.7188 |      - | 252.32 KB |       57.26 |
 Needlr_SourceGen_ToServiceProvider  | 105.885 μs |  18.4376 μs |   4.7882 μs |  86.29 |    3.64 |    2 | 10.7422 | 0.9766 | 183.42 KB |       41.63 |
 Needlr_SourceGen_ToProvider         | 107.061 μs |   6.3555 μs |   0.9835 μs |  87.25 |    1.05 |    2 | 11.2305 | 1.4648 | 188.09 KB |       42.69 |
