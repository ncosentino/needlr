```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                                                      | Mean        | Error      | StdDev    | Ratio | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
|------------------------------------------------------------ |------------:|-----------:|----------:|------:|--------:|-----:|--------:|-------:|----------:|------------:|
| BuildServiceProvider_Reflection                             | 2,104.46 μs | 352.355 μs | 54.527 μs |  1.00 |    0.03 |    3 | 54.6875 | 7.8125 | 896.96 KB |        1.00 |
| BuildServiceProvider_SourceGenExplicit_AssemblyListProvided |    80.09 μs |   1.178 μs |  0.182 μs |  0.04 |    0.00 |    1 |  6.4697 | 0.8545 | 105.84 KB |        0.12 |
| BuildServiceProvider_SourceGenImplicit_AssemblyListProvided |   106.30 μs |   2.097 μs |  0.325 μs |  0.05 |    0.00 |    2 | 10.2539 | 1.4648 | 174.56 KB |        0.19 |
| BuildServiceProvider_SourceGenExplicit_NoAssemblyList       |    79.63 μs |   2.857 μs |  0.442 μs |  0.04 |    0.00 |    1 |  6.3477 | 0.4883 | 105.84 KB |        0.12 |
| BuildServiceProvider_SourceGenImplicit_NoAssemblyList       |   105.56 μs |   1.638 μs |  0.425 μs |  0.05 |    0.00 |    2 | 10.2539 | 1.4648 | 174.56 KB |        0.19 |
