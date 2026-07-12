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
| ManualDI_BuildWebApp                 | 2.892 ms | 0.3440 ms | 0.0893 ms |  1.00 |    0.04 |    1 |  519.05 KB |        1.00 |
| Needlr_Reflection_BuildWebApp        | 9.518 ms | 0.4510 ms | 0.1171 ms |  3.29 |    0.10 |    2 | 1776.68 KB |        3.42 |
| Needlr_SourceGen_BuildWebApp         | 3.645 ms | 0.1335 ms | 0.0347 ms |  1.26 |    0.04 |    1 |  812.28 KB |        1.56 |
| Needlr_SourceGenExplicit_BuildWebApp | 3.573 ms | 0.1897 ms | 0.0493 ms |  1.24 |    0.04 |    1 |  734.22 KB |        1.41 |
