```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                          | Mean           | Error        | StdDev       | Ratio     | RatioSD | Rank | Gen0    | Gen1   | Allocated | Alloc Ratio |
|-------------------------------- |---------------:|-------------:|-------------:|----------:|--------:|-----:|--------:|-------:|----------:|------------:|
| ManualDI_RegisterTypes          |       102.5 ns |      8.44 ns |      2.19 ns |      1.00 |    0.03 |    1 |  0.0315 |      - |     528 B |        1.00 |
| Needlr_Reflection_RegisterTypes | 1,207,035.9 ns | 38,810.24 ns | 10,078.90 ns | 11,776.57 |  245.90 |    3 | 35.1563 | 3.9063 |  589218 B |    1,115.94 |
| Needlr_SourceGen_RegisterTypes  |    17,691.9 ns |    782.50 ns |    121.09 ns |    172.61 |    3.53 |    2 |  1.8005 | 0.1221 |   30440 B |       57.65 |
