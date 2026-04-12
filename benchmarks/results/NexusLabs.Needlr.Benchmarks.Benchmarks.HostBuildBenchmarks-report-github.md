```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.201
  [Host]   : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

```
| Method                             | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated  | Alloc Ratio |
|----------------------------------- |---------:|----------:|----------:|------:|--------:|-----:|-----------:|------------:|
| ManualDI_BuildHost                 | 2.174 ms | 0.1572 ms | 0.0243 ms |  1.00 |    0.01 |    1 |  317.56 KB |        1.00 |
| Needlr_Reflection_BuildHost        | 7.854 ms | 0.1038 ms | 0.0161 ms |  3.61 |    0.04 |    3 | 1556.63 KB |        4.90 |
| Needlr_SourceGen_BuildHost         | 2.838 ms | 0.0828 ms | 0.0215 ms |  1.31 |    0.02 |    2 |  583.68 KB |        1.84 |
| Needlr_SourceGenExplicit_BuildHost | 2.799 ms | 0.0296 ms | 0.0077 ms |  1.29 |    0.01 |    2 |  506.63 KB |        1.60 |
