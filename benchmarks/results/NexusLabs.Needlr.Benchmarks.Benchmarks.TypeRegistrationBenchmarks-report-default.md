
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                          | Mean           | Error           | StdDev        | Ratio     | RatioSD  | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
-------------------------------- |---------------:|----------------:|--------------:|----------:|---------:|-----:|--------:|-------:|----------:|------------:|
 ManualDI_RegisterTypes          |       104.7 ns |         8.85 ns |       2.30 ns |      1.00 |     0.03 |    1 |  0.0315 |      - |     528 B |        1.00 |
 Needlr_Reflection_RegisterTypes | 1,741,748.8 ns | 2,290,793.91 ns | 594,911.91 ns | 16,649.04 | 5,202.78 |    3 | 31.2500 |      - |  589831 B |    1,117.10 |
 Needlr_SourceGen_RegisterTypes  |    17,792.5 ns |       649.60 ns |     168.70 ns |    170.08 |     3.70 |    2 |  1.8005 | 0.1221 |   30440 B |       57.65 |
