
BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                                                      | Mean        | Error      | StdDev    | Ratio | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
------------------------------------------------------------ |------------:|-----------:|----------:|------:|--------:|-----:|--------:|-------:|----------:|------------:|
 BuildServiceProvider_Reflection                             | 1,257.05 μs | 119.732 μs | 18.529 μs |  1.00 |    0.02 |    3 | 31.2500 |      - | 524.96 KB |        1.00 |
 BuildServiceProvider_SourceGenExplicit_AssemblyListProvided |    64.74 μs |   1.311 μs |  0.203 μs |  0.05 |    0.00 |    1 |  4.8828 | 0.4883 |  87.12 KB |        0.17 |
 BuildServiceProvider_SourceGenImplicit_AssemblyListProvided |    85.22 μs |   0.853 μs |  0.221 μs |  0.07 |    0.00 |    2 |  8.3008 | 0.9766 | 139.39 KB |        0.27 |
 BuildServiceProvider_SourceGenExplicit_NoAssemblyList       |    66.03 μs |   0.395 μs |  0.102 μs |  0.05 |    0.00 |    1 |  5.3711 | 0.4883 |  89.47 KB |        0.17 |
 BuildServiceProvider_SourceGenImplicit_NoAssemblyList       |    84.19 μs |   1.051 μs |  0.163 μs |  0.07 |    0.00 |    2 |  8.3008 | 0.4883 | 139.43 KB |        0.27 |
