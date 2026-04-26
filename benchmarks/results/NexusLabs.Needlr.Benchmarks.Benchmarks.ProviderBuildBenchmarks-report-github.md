```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                              | Mean       | Error       | StdDev     | Ratio  | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
|------------------------------------ |-----------:|------------:|-----------:|-------:|--------:|-----:|--------:|-------:|----------:|------------:|
| ManualDI_ToServiceProvider          |   1.186 μs |   0.0815 μs |  0.0212 μs |   1.00 |    0.02 |    1 |  0.2689 | 0.0877 |   4.41 KB |        1.00 |
| Needlr_Reflection_ToServiceProvider | 827.059 μs | 439.7687 μs | 68.0547 μs | 697.60 |   52.30 |    3 | 11.7188 |      - | 252.31 KB |       57.26 |
| Needlr_SourceGen_ToServiceProvider  | 112.431 μs |   1.0057 μs |  0.1556 μs |  94.83 |    1.57 |    2 | 11.2305 | 1.4648 |  186.7 KB |       42.37 |
| Needlr_SourceGen_ToProvider         | 125.333 μs |  34.8163 μs |  9.0417 μs | 105.71 |    7.18 |    2 | 10.7422 | 0.9766 | 191.38 KB |       43.43 |
