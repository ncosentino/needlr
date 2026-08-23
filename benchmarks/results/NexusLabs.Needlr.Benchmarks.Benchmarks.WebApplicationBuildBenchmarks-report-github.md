```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

```
| Method                               | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated  | Alloc Ratio |
|------------------------------------- |---------:|----------:|----------:|------:|--------:|-----:|-----------:|------------:|
| ManualDI_BuildWebApp                 | 2.703 ms | 0.1244 ms | 0.0193 ms |  1.00 |    0.01 |    1 |  521.65 KB |        1.00 |
| Needlr_Reflection_BuildWebApp        | 8.968 ms | 0.4971 ms | 0.0769 ms |  3.32 |    0.03 |    3 | 1778.72 KB |        3.41 |
| Needlr_SourceGen_BuildWebApp         | 3.539 ms | 0.1010 ms | 0.0262 ms |  1.31 |    0.01 |    2 |  803.13 KB |        1.54 |
| Needlr_SourceGenExplicit_BuildWebApp | 3.479 ms | 0.0899 ms | 0.0234 ms |  1.29 |    0.01 |    2 |  726.09 KB |        1.39 |
