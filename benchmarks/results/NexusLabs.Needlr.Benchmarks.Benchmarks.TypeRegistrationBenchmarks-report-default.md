
BenchmarkDotNet v0.14.0, Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.404
  [Host]   : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                          | Mean           | Error        | StdDev      | Ratio     | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
-------------------------------- |---------------:|-------------:|------------:|----------:|--------:|-----:|--------:|-------:|----------:|------------:|
 ManualDI_RegisterTypes          |       109.4 ns |      1.24 ns |     0.32 ns |      1.00 |    0.00 |    1 |  0.0315 |      - |     528 B |        1.00 |
 Needlr_Reflection_RegisterTypes | 1,236,650.0 ns | 14,548.99 ns | 2,251.47 ns | 11,306.35 |   35.60 |    3 | 35.1563 |      - |  589442 B |    1,116.37 |
 Needlr_SourceGen_RegisterTypes  |    18,135.1 ns |    202.40 ns |    52.56 ns |    165.80 |    0.63 |    2 |  1.8311 | 0.1221 |   30960 B |       58.64 |
