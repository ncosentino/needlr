
BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                          | Mean           | Error        | StdDev      | Ratio     | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
-------------------------------- |---------------:|-------------:|------------:|----------:|--------:|-----:|--------:|-------:|----------:|------------:|
 ManualDI_RegisterTypes          |       101.8 ns |      2.66 ns |     0.41 ns |      1.00 |    0.01 |    1 |  0.0315 |      - |     528 B |        1.00 |
 Needlr_Reflection_RegisterTypes | 1,209,973.8 ns | 35,445.09 ns | 9,204.98 ns | 11,882.36 |   93.28 |    3 | 35.1563 | 3.9063 |  589901 B |    1,117.24 |
 Needlr_SourceGen_RegisterTypes  |    17,358.0 ns |    191.11 ns |    49.63 ns |    170.46 |    0.76 |    2 |  1.8005 | 0.1221 |   30440 B |       57.65 |
