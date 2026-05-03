
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                          | Mean            | Error          | StdDev         | Ratio     | RatioSD  | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
-------------------------------- |----------------:|---------------:|---------------:|----------:|---------:|-----:|--------:|-------:|----------:|------------:|
 ManualDI_RegisterTypes          |        93.92 ns |       3.139 ns |       0.486 ns |      1.00 |     0.01 |    1 |  0.0315 |      - |     528 B |        1.00 |
 Needlr_Reflection_RegisterTypes | 1,305,059.67 ns | 913,646.004 ns | 141,387.730 ns | 13,895.98 | 1,348.08 |    3 | 31.2500 |      - |  589831 B |    1,117.10 |
 Needlr_SourceGen_RegisterTypes  |    17,018.43 ns |     532.527 ns |      82.409 ns |    181.21 |     1.15 |    2 |  1.8005 | 0.1221 |   30440 B |       57.65 |
