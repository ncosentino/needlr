```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                                                      | Mean        | Error     | StdDev   | Ratio | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
|------------------------------------------------------------ |------------:|----------:|---------:|------:|-----:|--------:|-------:|----------:|------------:|
| BuildServiceProvider_Reflection                             | 1,242.01 μs | 39.471 μs | 6.108 μs |  1.00 |    3 | 31.2500 |      - | 524.83 KB |        1.00 |
| BuildServiceProvider_SourceGenExplicit_AssemblyListProvided |    61.57 μs |  1.668 μs | 0.433 μs |  0.05 |    1 |  4.8828 | 0.4883 |  87.08 KB |        0.17 |
| BuildServiceProvider_SourceGenImplicit_AssemblyListProvided |    82.60 μs |  3.438 μs | 0.893 μs |  0.07 |    2 |  8.3008 | 0.9766 | 139.39 KB |        0.27 |
| BuildServiceProvider_SourceGenExplicit_NoAssemblyList       |    62.58 μs |  1.386 μs | 0.360 μs |  0.05 |    1 |  4.8828 | 0.4883 |  87.12 KB |        0.17 |
| BuildServiceProvider_SourceGenImplicit_NoAssemblyList       |    82.86 μs |  2.902 μs | 0.754 μs |  0.07 |    2 |  8.3008 | 0.9766 | 141.73 KB |        0.27 |
