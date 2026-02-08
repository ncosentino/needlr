```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

```
| Method                               | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated  | Alloc Ratio |
|------------------------------------- |---------:|----------:|----------:|------:|--------:|-----:|-----------:|------------:|
| ManualDI_BuildWebApp                 | 2.701 ms | 0.0944 ms | 0.0146 ms |  1.00 |    0.01 |    1 |  518.67 KB |        1.00 |
| Needlr_Reflection_BuildWebApp        | 9.036 ms | 0.2487 ms | 0.0646 ms |  3.35 |    0.03 |    2 | 1748.34 KB |        3.37 |
| Needlr_SourceGen_BuildWebApp         | 3.388 ms | 0.2296 ms | 0.0596 ms |  1.25 |    0.02 |    1 |  736.38 KB |        1.42 |
| Needlr_SourceGenExplicit_BuildWebApp | 3.327 ms | 0.1834 ms | 0.0476 ms |  1.23 |    0.02 |    1 |  676.55 KB |        1.30 |
