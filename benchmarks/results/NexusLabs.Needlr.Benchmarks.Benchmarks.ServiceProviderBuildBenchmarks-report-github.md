```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
Intel Core 7 150U, 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                                        | Mean         | Error         | StdDev      | Ratio    | RatioSD | Rank | Gen0     | Gen1    | Allocated | Alloc Ratio |
|---------------------------------------------- |-------------:|--------------:|------------:|---------:|--------:|-----:|---------:|--------:|----------:|------------:|
| ManualDI_BuildServiceProvider                 |     1.263 μs |     0.2172 μs |   0.0336 μs |     1.00 |    0.03 |    1 |   0.9270 |  0.1011 |   5.69 KB |        1.00 |
| Needlr_Reflection_BuildServiceProvider        | 4,313.598 μs | 1,570.3054 μs | 407.8033 μs | 3,417.33 |  307.53 |    4 | 156.2500 | 15.6250 | 964.27 KB |      169.54 |
| Needlr_SourceGenExplicit_BuildServiceProvider |    63.863 μs |     8.0285 μs |   2.0850 μs |    50.59 |    1.94 |    2 |  17.5781 |  1.9531 | 108.67 KB |       19.11 |
| Needlr_SourceGenImplicit_BuildServiceProvider |    85.571 μs |    27.5077 μs |   7.1437 μs |    67.79 |    5.44 |    3 |  30.2734 |  3.9063 | 185.89 KB |       32.68 |
