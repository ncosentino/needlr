
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                          | Mean            | Error            | StdDev         | Ratio     | RatioSD  | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
-------------------------------- |----------------:|-----------------:|---------------:|----------:|---------:|-----:|--------:|-------:|----------:|------------:|
 ManualDI_RegisterTypes          |        98.09 ns |         3.832 ns |       0.995 ns |      1.00 |     0.01 |    1 |  0.0315 |      - |     528 B |        1.00 |
 Needlr_Reflection_RegisterTypes | 1,923,424.53 ns | 2,507,997.573 ns | 651,319.007 ns | 19,610.03 | 6,064.89 |    3 | 31.2500 |      - |  589831 B |    1,117.10 |
 Needlr_SourceGen_RegisterTypes  |    17,939.39 ns |       144.705 ns |      37.580 ns |    182.90 |     1.74 |    2 |  1.8005 | 0.1221 |   30440 B |       57.65 |
