
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
12th Gen Intel Core i7-12700H, 1 CPU, 20 logical and 10 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                                        | Mean         | Error         | StdDev      | Ratio    | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
---------------------------------------------- |-------------:|--------------:|------------:|---------:|--------:|-----:|--------:|-------:|----------:|------------:|
 ManualDI_BuildServiceProvider                 |     1.679 μs |     0.2388 μs |   0.0620 μs |     1.00 |    0.05 |    1 |  0.5550 | 0.0916 |   6.81 KB |        1.00 |
 Needlr_Reflection_BuildServiceProvider        | 2,186.554 μs | 1,443.3742 μs | 223.3638 μs | 1,303.70 |  126.49 |    3 | 70.3125 | 7.8125 | 931.86 KB |      136.79 |
 Needlr_SourceGenExplicit_BuildServiceProvider |    76.581 μs |    43.2630 μs |   6.6950 μs |    45.66 |    3.88 |    2 |  8.7891 | 0.9766 | 109.57 KB |       16.08 |
 Needlr_SourceGenImplicit_BuildServiceProvider |    91.116 μs |    17.0287 μs |   2.6352 μs |    54.33 |    2.32 |    2 | 15.1367 | 1.9531 | 189.89 KB |       27.87 |
