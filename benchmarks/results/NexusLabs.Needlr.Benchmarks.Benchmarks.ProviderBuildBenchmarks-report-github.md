```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                              | Mean       | Error       | StdDev     | Ratio  | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
|------------------------------------ |-----------:|------------:|-----------:|-------:|--------:|-----:|--------:|-------:|----------:|------------:|
| ManualDI_ToServiceProvider          |   1.240 μs |   0.1262 μs |  0.0328 μs |   1.00 |    0.03 |    1 |  0.2689 | 0.0877 |   4.41 KB |        1.00 |
| Needlr_Reflection_ToServiceProvider | 819.600 μs | 450.0383 μs | 69.6439 μs | 661.58 |   52.40 |    3 | 11.7188 |      - | 252.32 KB |       57.26 |
| Needlr_SourceGen_ToServiceProvider  | 115.307 μs |   1.3476 μs |  0.2085 μs |  93.08 |    2.22 |    2 | 11.2305 | 1.4648 |  186.7 KB |       42.37 |
| Needlr_SourceGen_ToProvider         | 131.573 μs |  51.4160 μs | 13.3526 μs | 106.21 |   10.16 |    2 | 10.7422 | 0.9766 | 191.38 KB |       43.43 |
