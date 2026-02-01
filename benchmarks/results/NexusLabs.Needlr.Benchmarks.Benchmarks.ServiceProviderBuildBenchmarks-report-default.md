
BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                                                      | Mean        | Error        | StdDev     | Ratio | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
------------------------------------------------------------ |------------:|-------------:|-----------:|------:|--------:|-----:|--------:|-------:|----------:|------------:|
 BuildServiceProvider_Reflection                             | 1,782.66 μs | 1,018.724 μs | 157.649 μs |  1.01 |    0.11 |    3 | 39.0625 |      - | 722.53 KB |        1.00 |
 BuildServiceProvider_SourceGenExplicit_AssemblyListProvided |    69.97 μs |     0.191 μs |   0.050 μs |  0.04 |    0.00 |    1 |  5.8594 | 0.7324 |  95.96 KB |        0.13 |
 BuildServiceProvider_SourceGenImplicit_AssemblyListProvided |    95.13 μs |     2.861 μs |   0.743 μs |  0.05 |    0.00 |    2 | 10.2539 | 1.4648 | 167.56 KB |        0.23 |
 BuildServiceProvider_SourceGenExplicit_NoAssemblyList       |    70.44 μs |     0.565 μs |   0.147 μs |  0.04 |    0.00 |    1 |  5.8594 | 0.4883 |  95.97 KB |        0.13 |
 BuildServiceProvider_SourceGenImplicit_NoAssemblyList       |    95.86 μs |     2.434 μs |   0.377 μs |  0.05 |    0.00 |    2 | 10.2539 | 1.4648 | 167.56 KB |        0.23 |
