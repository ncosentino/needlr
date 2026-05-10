```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

```
| Method                             | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated  | Alloc Ratio |
|----------------------------------- |---------:|----------:|----------:|------:|--------:|-----:|-----------:|------------:|
| ManualDI_BuildHost                 | 2.279 ms | 0.1494 ms | 0.0231 ms |  1.00 |    0.01 |    1 |  317.23 KB |        1.00 |
| Needlr_Reflection_BuildHost        | 7.816 ms | 0.2603 ms | 0.0676 ms |  3.43 |    0.04 |    2 | 1556.63 KB |        4.91 |
| Needlr_SourceGen_BuildHost         | 2.814 ms | 0.0435 ms | 0.0113 ms |  1.23 |    0.01 |    1 |  583.68 KB |        1.84 |
| Needlr_SourceGenExplicit_BuildHost | 2.754 ms | 0.0517 ms | 0.0134 ms |  1.21 |    0.01 |    1 |  506.63 KB |        1.60 |
