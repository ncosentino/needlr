```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

```
| Method                               | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated  | Alloc Ratio |
|------------------------------------- |---------:|----------:|----------:|------:|--------:|-----:|-----------:|------------:|
| ManualDI_BuildWebApp                 | 2.777 ms | 0.0855 ms | 0.0132 ms |  1.00 |    0.01 |    1 |  519.34 KB |        1.00 |
| Needlr_Reflection_BuildWebApp        | 8.911 ms | 0.2438 ms | 0.0377 ms |  3.21 |    0.02 |    3 | 1776.68 KB |        3.42 |
| Needlr_SourceGen_BuildWebApp         | 3.545 ms | 0.1017 ms | 0.0264 ms |  1.28 |    0.01 |    2 |  812.28 KB |        1.56 |
| Needlr_SourceGenExplicit_BuildWebApp | 3.483 ms | 0.1715 ms | 0.0445 ms |  1.25 |    0.02 |    2 |  734.22 KB |        1.41 |
