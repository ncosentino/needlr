```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                              | Mean       | Error      | StdDev    | Ratio  | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
|------------------------------------ |-----------:|-----------:|----------:|-------:|--------:|-----:|--------:|-------:|----------:|------------:|
| ManualDI_ToServiceProvider          |   1.211 μs |  0.0355 μs | 0.0092 μs |   1.00 |    0.01 |    1 |  0.2689 | 0.0877 |   4.41 KB |        1.00 |
| Needlr_Reflection_ToServiceProvider | 766.939 μs | 14.8434 μs | 2.2970 μs | 633.35 |    4.73 |    3 | 13.6719 |      - | 252.39 KB |       57.28 |
| Needlr_SourceGen_ToServiceProvider  | 112.686 μs |  0.7238 μs | 0.1880 μs |  93.06 |    0.66 |    2 | 11.3525 | 1.5869 | 187.35 KB |       42.52 |
| Needlr_SourceGen_ToProvider         | 127.105 μs | 32.9887 μs | 8.5671 μs | 104.97 |    6.50 |    2 | 10.7422 | 0.9766 | 188.75 KB |       42.84 |
