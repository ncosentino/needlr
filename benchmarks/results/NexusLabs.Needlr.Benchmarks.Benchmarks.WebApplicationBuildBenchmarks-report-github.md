```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.201
  [Host]   : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

```
| Method                               | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated  | Alloc Ratio |
|------------------------------------- |---------:|----------:|----------:|------:|--------:|-----:|-----------:|------------:|
| ManualDI_BuildWebApp                 | 2.806 ms | 0.1741 ms | 0.0452 ms |  1.00 |    0.02 |    1 |  518.63 KB |        1.00 |
| Needlr_Reflection_BuildWebApp        | 8.655 ms | 0.2593 ms | 0.0673 ms |  3.09 |    0.05 |    2 | 1775.64 KB |        3.42 |
| Needlr_SourceGen_BuildWebApp         | 3.537 ms | 0.1415 ms | 0.0367 ms |  1.26 |    0.02 |    1 |  811.58 KB |        1.56 |
| Needlr_SourceGenExplicit_BuildWebApp | 3.460 ms | 0.0702 ms | 0.0109 ms |  1.23 |    0.02 |    1 |  733.52 KB |        1.41 |
