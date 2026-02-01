
BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                              | Mean       | Error      | StdDev     | Ratio  | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
------------------------------------ |-----------:|-----------:|-----------:|-------:|--------:|-----:|--------:|-------:|----------:|------------:|
 ManualDI_ToServiceProvider          |   1.194 μs |  0.0448 μs |  0.0069 μs |   1.00 |    0.01 |    1 |  0.2689 | 0.0877 |   4.41 KB |        1.00 |
 Needlr_Reflection_ToServiceProvider | 688.529 μs | 11.8512 μs |  1.8340 μs | 576.46 |    3.29 |    3 | 11.7188 |      - | 222.19 KB |       50.43 |
 Needlr_SourceGen_ToServiceProvider  | 115.040 μs |  6.9801 μs |  1.8127 μs |  96.32 |    1.48 |    2 | 10.7422 | 1.4648 | 177.46 KB |       40.28 |
 Needlr_SourceGen_ToProvider         | 126.062 μs | 45.4504 μs | 11.8033 μs | 105.54 |    9.08 |    2 | 10.7422 | 0.9766 | 182.15 KB |       41.34 |
