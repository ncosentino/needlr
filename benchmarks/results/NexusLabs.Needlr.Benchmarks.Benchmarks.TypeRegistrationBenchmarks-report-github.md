```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                          | Mean            | Error            | StdDev         | Ratio     | RatioSD  | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
|-------------------------------- |----------------:|-----------------:|---------------:|----------:|---------:|-----:|--------:|-------:|----------:|------------:|
| ManualDI_RegisterTypes          |        94.84 ns |         9.267 ns |       1.434 ns |      1.00 |     0.02 |    1 |  0.0315 |      - |     528 B |        1.00 |
| Needlr_Reflection_RegisterTypes | 1,412,518.41 ns | 1,084,264.341 ns | 167,791.107 ns | 14,896.32 | 1,595.66 |    3 | 31.2500 |      - |  590198 B |    1,117.80 |
| Needlr_SourceGen_RegisterTypes  |    18,146.61 ns |       613.182 ns |     159.241 ns |    191.37 |     3.00 |    2 |  1.8005 | 0.1221 |   30440 B |       57.65 |
