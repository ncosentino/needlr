
BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                              | Mean       | Error       | StdDev     | Ratio  | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
------------------------------------ |-----------:|------------:|-----------:|-------:|--------:|-----:|--------:|-------:|----------:|------------:|
 ManualDI_ToServiceProvider          |   1.166 μs |   0.0136 μs |  0.0035 μs |   1.00 |    0.00 |    1 |  0.2689 | 0.0877 |   4.41 KB |        1.00 |
 Needlr_Reflection_ToServiceProvider | 878.813 μs | 285.7464 μs | 74.2074 μs | 753.42 |   58.11 |    3 | 15.6250 |      - | 259.36 KB |       58.86 |
 Needlr_SourceGen_ToServiceProvider  | 105.526 μs |   2.1915 μs |  0.3391 μs |  90.47 |    0.36 |    2 | 10.2539 | 1.4648 | 174.56 KB |       39.62 |
 Needlr_SourceGen_ToProvider         | 110.907 μs |   1.5426 μs |  0.2387 μs |  95.08 |    0.32 |    2 | 10.7422 | 1.4648 | 179.24 KB |       40.68 |
