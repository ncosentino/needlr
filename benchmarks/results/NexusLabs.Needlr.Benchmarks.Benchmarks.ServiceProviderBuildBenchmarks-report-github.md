```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                                                      | Mean        | Error     | StdDev    | Ratio | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
|------------------------------------------------------------ |------------:|----------:|----------:|------:|-----:|--------:|-------:|----------:|------------:|
| BuildServiceProvider_Reflection                             | 1,240.32 μs | 76.695 μs | 11.869 μs |  1.00 |    3 | 31.2500 |      - | 524.79 KB |        1.00 |
| BuildServiceProvider_SourceGenExplicit_AssemblyListProvided |    63.91 μs |  2.953 μs |  0.767 μs |  0.05 |    1 |  5.3711 | 0.4883 |  89.46 KB |        0.17 |
| BuildServiceProvider_SourceGenImplicit_AssemblyListProvided |    83.62 μs |  2.349 μs |  0.610 μs |  0.07 |    2 |  8.3008 | 0.9766 | 139.39 KB |        0.27 |
| BuildServiceProvider_SourceGenExplicit_NoAssemblyList       |    61.68 μs |  2.444 μs |  0.635 μs |  0.05 |    1 |  4.8828 | 0.4883 |  87.08 KB |        0.17 |
| BuildServiceProvider_SourceGenImplicit_NoAssemblyList       |    84.82 μs |  4.200 μs |  0.650 μs |  0.07 |    2 |  8.3008 | 0.9766 | 141.73 KB |        0.27 |
