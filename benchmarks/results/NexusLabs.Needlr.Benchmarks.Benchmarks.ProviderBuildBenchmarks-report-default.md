
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                              | Mean       | Error       | StdDev     | Ratio  | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
------------------------------------ |-----------:|------------:|-----------:|-------:|--------:|-----:|--------:|-------:|----------:|------------:|
 ManualDI_ToServiceProvider          |   1.190 μs |   0.0479 μs |  0.0124 μs |   1.00 |    0.01 |    1 |  0.2689 | 0.0877 |   4.41 KB |        1.00 |
 Needlr_Reflection_ToServiceProvider | 796.955 μs | 250.8283 μs | 38.8160 μs | 669.97 |   29.69 |    3 | 11.7188 |      - | 252.32 KB |       57.26 |
 Needlr_SourceGen_ToServiceProvider  | 110.508 μs |   1.7796 μs |  0.4621 μs |  92.90 |    0.95 |    2 | 10.7422 | 0.9766 | 183.41 KB |       41.62 |
 Needlr_SourceGen_ToProvider         | 141.091 μs | 122.5207 μs | 31.8182 μs | 118.61 |   24.44 |    2 | 10.7422 | 0.9766 | 188.09 KB |       42.69 |
