```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                          | Mean           | Error           | StdDev        | Ratio     | RatioSD  | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
|-------------------------------- |---------------:|----------------:|--------------:|----------:|---------:|-----:|--------:|-------:|----------:|------------:|
| ManualDI_RegisterTypes          |       103.7 ns |         2.45 ns |       0.64 ns |      1.00 |     0.01 |    1 |  0.0315 |      - |     528 B |        1.00 |
| Needlr_Reflection_RegisterTypes | 1,870,791.6 ns | 2,794,824.57 ns | 725,807.07 ns | 18,038.49 | 6,389.50 |    3 | 31.2500 |      - |  589831 B |    1,117.10 |
| Needlr_SourceGen_RegisterTypes  |    15,087.5 ns |       545.66 ns |     141.71 ns |    145.48 |     1.49 |    2 |  1.8158 | 0.1373 |   30440 B |       57.65 |
