
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                              | Mean       | Error      | StdDev     | Ratio  | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
------------------------------------ |-----------:|-----------:|-----------:|-------:|--------:|-----:|--------:|-------:|----------:|------------:|
 ManualDI_ToServiceProvider          |   1.160 μs |  0.0663 μs |  0.0172 μs |   1.00 |    0.02 |    1 |  0.2689 | 0.0877 |   4.41 KB |        1.00 |
 Needlr_Reflection_ToServiceProvider | 739.424 μs |  6.2014 μs |  0.9597 μs | 637.69 |    8.73 |    3 | 13.6719 |      - | 252.31 KB |       57.26 |
 Needlr_SourceGen_ToServiceProvider  |  95.530 μs |  3.4820 μs |  0.5388 μs |  82.39 |    1.20 |    2 | 10.7422 | 0.9766 | 183.41 KB |       41.62 |
 Needlr_SourceGen_ToProvider         | 119.003 μs | 41.3474 μs | 10.7378 μs | 102.63 |    8.57 |    2 | 10.7422 | 0.9766 | 191.38 KB |       43.43 |
