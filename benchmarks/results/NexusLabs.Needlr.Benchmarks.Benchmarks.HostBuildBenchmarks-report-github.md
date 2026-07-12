```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

```
| Method                             | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated  | Alloc Ratio |
|----------------------------------- |---------:|----------:|----------:|------:|--------:|-----:|-----------:|------------:|
| ManualDI_BuildHost                 | 2.139 ms | 0.2472 ms | 0.0383 ms |  1.00 |    0.02 |    1 |  318.03 KB |        1.00 |
| Needlr_Reflection_BuildHost        | 8.294 ms | 0.3146 ms | 0.0817 ms |  3.88 |    0.07 |    3 | 1557.18 KB |        4.90 |
| Needlr_SourceGen_BuildHost         | 2.887 ms | 0.2038 ms | 0.0529 ms |  1.35 |    0.03 |    2 |  584.23 KB |        1.84 |
| Needlr_SourceGenExplicit_BuildHost | 2.765 ms | 0.0784 ms | 0.0121 ms |  1.29 |    0.02 |    2 |  507.17 KB |        1.59 |
