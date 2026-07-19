```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

```
| Method                             | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated  | Alloc Ratio |
|----------------------------------- |---------:|----------:|----------:|------:|--------:|-----:|-----------:|------------:|
| ManualDI_BuildHost                 | 2.392 ms | 0.6920 ms | 0.1797 ms |  1.00 |    0.10 |    1 |  318.03 KB |        1.00 |
| Needlr_Reflection_BuildHost        | 8.224 ms | 0.3974 ms | 0.1032 ms |  3.45 |    0.23 |    2 | 1557.18 KB |        4.90 |
| Needlr_SourceGen_BuildHost         | 2.893 ms | 0.2222 ms | 0.0577 ms |  1.21 |    0.08 |    1 |  584.23 KB |        1.84 |
| Needlr_SourceGenExplicit_BuildHost | 2.843 ms | 0.1798 ms | 0.0467 ms |  1.19 |    0.08 |    1 |  507.17 KB |        1.59 |
