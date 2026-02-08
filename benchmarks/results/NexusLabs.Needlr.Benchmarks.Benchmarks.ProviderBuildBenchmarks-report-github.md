```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                              | Mean       | Error       | StdDev      | Ratio  | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
|------------------------------------ |-----------:|------------:|------------:|-------:|--------:|-----:|--------:|-------:|----------:|------------:|
| ManualDI_ToServiceProvider          |   1.225 μs |   0.0867 μs |   0.0225 μs |   1.00 |    0.02 |    1 |  0.2689 | 0.0877 |   4.41 KB |        1.00 |
| Needlr_Reflection_ToServiceProvider | 902.008 μs | 452.7428 μs | 117.5759 μs | 736.68 |   88.53 |    3 | 15.6250 |      - | 259.36 KB |       58.86 |
| Needlr_SourceGen_ToServiceProvider  | 107.714 μs |   1.2043 μs |   0.1864 μs |  87.97 |    1.48 |    2 | 10.7422 | 0.9766 | 177.48 KB |       40.28 |
| Needlr_SourceGen_ToProvider         | 112.275 μs |   5.2679 μs |   0.8152 μs |  91.70 |    1.65 |    2 | 10.7422 | 0.9766 | 182.13 KB |       41.34 |
