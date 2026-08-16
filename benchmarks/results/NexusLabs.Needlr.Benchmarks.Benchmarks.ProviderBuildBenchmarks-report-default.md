
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
Intel Core 7 150U, 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                              | Mean       | Error       | StdDev     | Ratio  | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
------------------------------------ |-----------:|------------:|-----------:|-------:|--------:|-----:|--------:|-------:|----------:|------------:|
 ManualDI_ToServiceProvider          |   1.224 μs |   0.5596 μs |  0.1453 μs |   1.01 |    0.15 |    1 |  0.9022 | 0.0992 |   5.53 KB |        1.00 |
 Needlr_Reflection_ToServiceProvider | 533.567 μs | 103.7067 μs | 26.9323 μs | 440.62 |   49.78 |    3 | 41.0156 | 1.9531 | 260.04 KB |       47.01 |
 Needlr_SourceGen_ToServiceProvider  |  85.837 μs |   6.4924 μs |  1.6860 μs |  70.89 |    7.42 |    2 | 30.2734 | 3.9063 | 185.89 KB |       33.61 |
 Needlr_SourceGen_ToProvider         | 109.529 μs |  92.1282 μs | 23.9254 μs |  90.45 |   20.38 |    2 | 30.7617 | 3.9063 | 190.58 KB |       34.45 |
