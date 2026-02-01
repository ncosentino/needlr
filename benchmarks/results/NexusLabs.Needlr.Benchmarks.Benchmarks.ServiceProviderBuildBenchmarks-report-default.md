
BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                                                      | Mean        | Error      | StdDev    | Ratio | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
------------------------------------------------------------ |------------:|-----------:|----------:|------:|--------:|-----:|--------:|-------:|----------:|------------:|
 BuildServiceProvider_Reflection                             | 1,239.16 μs | 164.830 μs | 25.508 μs |  1.00 |    0.03 |    3 | 31.2500 |      - | 525.03 KB |        1.00 |
 BuildServiceProvider_SourceGenExplicit_AssemblyListProvided |    63.51 μs |   2.984 μs |  0.775 μs |  0.05 |    0.00 |    1 |  5.3711 | 0.4883 |  89.43 KB |        0.17 |
 BuildServiceProvider_SourceGenImplicit_AssemblyListProvided |    81.82 μs |   2.267 μs |  0.351 μs |  0.07 |    0.00 |    2 |  8.3008 | 0.9766 | 141.73 KB |        0.27 |
 BuildServiceProvider_SourceGenExplicit_NoAssemblyList       |    61.53 μs |   1.203 μs |  0.312 μs |  0.05 |    0.00 |    1 |  5.3711 | 0.4883 |  89.47 KB |        0.17 |
 BuildServiceProvider_SourceGenImplicit_NoAssemblyList       |    82.00 μs |   0.956 μs |  0.248 μs |  0.07 |    0.00 |    2 |  8.3008 | 0.4883 | 139.43 KB |        0.27 |
