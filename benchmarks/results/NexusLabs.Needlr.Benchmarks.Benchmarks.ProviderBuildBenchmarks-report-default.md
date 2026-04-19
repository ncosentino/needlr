
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.202
  [Host]   : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                              | Mean       | Error       | StdDev     | Ratio  | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
------------------------------------ |-----------:|------------:|-----------:|-------:|--------:|-----:|--------:|-------:|----------:|------------:|
 ManualDI_ToServiceProvider          |   1.233 μs |   0.0522 μs |  0.0136 μs |   1.00 |    0.01 |    1 |  0.2689 | 0.0877 |   4.41 KB |        1.00 |
 Needlr_Reflection_ToServiceProvider | 770.839 μs | 293.0408 μs | 45.3484 μs | 625.19 |   33.29 |    3 | 11.7188 |      - | 252.31 KB |       57.26 |
 Needlr_SourceGen_ToServiceProvider  |  97.307 μs |   4.6049 μs |  0.7126 μs |  78.92 |    0.95 |    2 | 10.7422 | 0.9766 | 183.41 KB |       41.62 |
 Needlr_SourceGen_ToProvider         | 103.157 μs |   1.2533 μs |  0.3255 μs |  83.67 |    0.88 |    2 | 11.2305 | 1.4648 | 188.09 KB |       42.69 |
