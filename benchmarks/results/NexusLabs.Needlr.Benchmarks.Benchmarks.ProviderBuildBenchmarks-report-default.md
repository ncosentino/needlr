
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
12th Gen Intel Core i7-12700H, 1 CPU, 20 logical and 10 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                              | Mean       | Error       | StdDev     | Ratio  | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
------------------------------------ |-----------:|------------:|-----------:|-------:|--------:|-----:|--------:|-------:|----------:|------------:|
 ManualDI_ToServiceProvider          |   1.678 μs |   0.3851 μs |  0.1000 μs |   1.00 |    0.08 |    1 |  0.5417 | 0.0896 |   6.66 KB |        1.00 |
 Needlr_Reflection_ToServiceProvider | 558.114 μs | 129.7920 μs | 33.7066 μs | 333.64 |   25.78 |    3 | 19.5313 |      - | 256.43 KB |       38.53 |
 Needlr_SourceGen_ToServiceProvider  |  99.491 μs |  53.7933 μs |  8.3246 μs |  59.48 |    5.48 |    2 | 15.1367 | 2.4414 | 186.61 KB |       28.03 |
 Needlr_SourceGen_ToProvider         | 103.699 μs |  33.1643 μs |  8.6127 μs |  61.99 |    5.78 |    2 | 15.6250 | 1.4648 | 195.98 KB |       29.44 |
