```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
Intel Xeon Platinum 8370C CPU 2.80GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                                                      | Mean        | Error      | StdDev    | Ratio | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
|------------------------------------------------------------ |------------:|-----------:|----------:|------:|--------:|-----:|--------:|-------:|----------:|------------:|
| BuildServiceProvider_Reflection                             | 1,214.63 μs | 121.419 μs | 18.790 μs |  1.00 |    0.02 |    3 | 19.5313 |      - |  522.7 KB |        1.00 |
| BuildServiceProvider_SourceGenExplicit_AssemblyListProvided |    61.82 μs |   1.699 μs |  0.441 μs |  0.05 |    0.00 |    1 |  3.4180 |      - |  87.06 KB |        0.17 |
| BuildServiceProvider_SourceGenImplicit_AssemblyListProvided |    78.76 μs |   2.307 μs |  0.599 μs |  0.06 |    0.00 |    2 |  5.3711 | 0.4883 | 141.73 KB |        0.27 |
| BuildServiceProvider_SourceGenExplicit_NoAssemblyList       |    60.08 μs |   2.245 μs |  0.347 μs |  0.05 |    0.00 |    1 |  3.4180 |      - |  87.06 KB |        0.17 |
| BuildServiceProvider_SourceGenImplicit_NoAssemblyList       |    80.33 μs |   1.223 μs |  0.318 μs |  0.07 |    0.00 |    2 |  5.3711 | 0.4883 | 141.69 KB |        0.27 |
