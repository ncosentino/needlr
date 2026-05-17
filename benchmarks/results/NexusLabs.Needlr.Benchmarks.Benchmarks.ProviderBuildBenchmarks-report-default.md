
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]   : .NET 10.0.8 (10.0.826.23019), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.8 (10.0.826.23019), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                              | Mean       | Error      | StdDev    | Ratio  | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
------------------------------------ |-----------:|-----------:|----------:|-------:|--------:|-----:|--------:|-------:|----------:|------------:|
 ManualDI_ToServiceProvider          |   1.388 μs |  0.1098 μs | 0.0285 μs |   1.00 |    0.03 |    1 |  0.2689 | 0.0877 |   4.41 KB |        1.00 |
 Needlr_Reflection_ToServiceProvider | 741.227 μs |  7.2079 μs | 1.1154 μs | 534.34 |   10.16 |    3 | 13.6719 |      - | 252.31 KB |       57.26 |
 Needlr_SourceGen_ToServiceProvider  | 105.449 μs |  1.5240 μs | 0.2358 μs |  76.02 |    1.45 |    2 | 11.2305 | 1.4648 |  186.7 KB |       42.37 |
 Needlr_SourceGen_ToProvider         | 118.522 μs | 59.1673 μs | 9.1562 μs |  85.44 |    6.09 |    2 | 10.7422 | 0.9766 | 188.09 KB |       42.69 |
