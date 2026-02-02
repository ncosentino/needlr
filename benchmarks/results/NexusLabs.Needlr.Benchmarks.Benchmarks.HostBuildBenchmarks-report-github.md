```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

```
| Method                             | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated  | Alloc Ratio |
|----------------------------------- |---------:|----------:|----------:|------:|--------:|-----:|-----------:|------------:|
| ManualDI_BuildHost                 | 2.070 ms | 0.1077 ms | 0.0167 ms |  1.00 |    0.01 |    1 |  316.94 KB |        1.00 |
| Needlr_Reflection_BuildHost        | 8.290 ms | 0.0603 ms | 0.0157 ms |  4.00 |    0.03 |    3 | 1582.79 KB |        4.99 |
| Needlr_SourceGen_BuildHost         | 2.755 ms | 0.0358 ms | 0.0055 ms |  1.33 |    0.01 |    2 |  565.88 KB |        1.79 |
| Needlr_SourceGenExplicit_BuildHost | 2.687 ms | 0.1508 ms | 0.0392 ms |  1.30 |    0.02 |    2 |  494.53 KB |        1.56 |
