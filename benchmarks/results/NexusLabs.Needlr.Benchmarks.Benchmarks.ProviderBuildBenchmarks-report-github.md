```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                              | Mean       | Error       | StdDev     | Ratio  | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
|------------------------------------ |-----------:|------------:|-----------:|-------:|--------:|-----:|--------:|-------:|----------:|------------:|
| ManualDI_ToServiceProvider          |   1.371 μs |   0.0647 μs |  0.0168 μs |   1.00 |    0.02 |    1 |  0.2689 | 0.0877 |   4.41 KB |        1.00 |
| Needlr_Reflection_ToServiceProvider | 804.686 μs | 230.3045 μs | 35.6399 μs | 586.90 |   24.03 |    3 | 11.7188 |      - | 252.41 KB |       57.28 |
| Needlr_SourceGen_ToServiceProvider  | 121.340 μs |   5.9670 μs |  1.5496 μs |  88.50 |    1.43 |    2 | 10.7422 | 0.9766 | 187.36 KB |       42.52 |
| Needlr_SourceGen_ToProvider         | 134.795 μs |  45.7376 μs | 11.8779 μs |  98.31 |    7.99 |    2 | 10.7422 | 0.9766 | 188.75 KB |       42.84 |
