
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                              | Mean       | Error       | StdDev      | Ratio  | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
------------------------------------ |-----------:|------------:|------------:|-------:|--------:|-----:|--------:|-------:|----------:|------------:|
 ManualDI_ToServiceProvider          |   1.312 μs |   0.0874 μs |   0.0227 μs |   1.00 |    0.02 |    1 |  0.2689 | 0.0877 |   4.41 KB |        1.00 |
 Needlr_Reflection_ToServiceProvider | 858.226 μs | 434.8743 μs | 112.9355 μs | 654.46 |   79.33 |    3 | 11.7188 |      - | 252.32 KB |       57.26 |
 Needlr_SourceGen_ToServiceProvider  | 113.648 μs |   4.2452 μs |   0.6569 μs |  86.66 |    1.47 |    2 | 10.7422 | 0.9766 |  186.7 KB |       42.37 |
 Needlr_SourceGen_ToProvider         | 131.130 μs |  67.3467 μs |  17.4897 μs | 100.00 |   12.28 |    2 | 10.7422 | 0.9766 | 188.09 KB |       42.69 |
