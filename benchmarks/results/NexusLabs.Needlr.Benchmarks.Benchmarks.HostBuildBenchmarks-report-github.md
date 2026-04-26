```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

```
| Method                             | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated  | Alloc Ratio |
|----------------------------------- |---------:|----------:|----------:|------:|--------:|-----:|-----------:|------------:|
| ManualDI_BuildHost                 | 2.324 ms | 0.4188 ms | 0.0648 ms |  1.00 |    0.04 |    1 |  317.56 KB |        1.00 |
| Needlr_Reflection_BuildHost        | 8.447 ms | 0.4907 ms | 0.1274 ms |  3.64 |    0.10 |    2 | 1556.63 KB |        4.90 |
| Needlr_SourceGen_BuildHost         | 2.842 ms | 0.2217 ms | 0.0343 ms |  1.22 |    0.03 |    1 |  583.68 KB |        1.84 |
| Needlr_SourceGenExplicit_BuildHost | 2.812 ms | 0.3765 ms | 0.0583 ms |  1.21 |    0.04 |    1 |  506.63 KB |        1.60 |
