```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

```
| Method                               | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|------------------------------------- |---------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
| ManualDI_BuildWebApp                 | 2.836 ms | 0.2257 ms | 0.0349 ms |  1.00 |    0.02 |    1 | 519.34 KB |        1.00 |
| Needlr_Reflection_BuildWebApp        | 9.456 ms | 2.7663 ms | 0.7184 ms |  3.34 |    0.24 |    2 | 1776.4 KB |        3.42 |
| Needlr_SourceGen_BuildWebApp         | 3.562 ms | 0.1975 ms | 0.0513 ms |  1.26 |    0.02 |    1 | 812.28 KB |        1.56 |
| Needlr_SourceGenExplicit_BuildWebApp | 3.517 ms | 0.1734 ms | 0.0450 ms |  1.24 |    0.02 |    1 | 734.22 KB |        1.41 |
