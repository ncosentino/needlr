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
| ManualDI_BuildHost                 | 2.203 ms | 0.4424 ms | 0.0685 ms |  1.00 |    0.04 |    1 |  318.03 KB |        1.00 |
| Needlr_Reflection_BuildHost        | 8.508 ms | 0.5087 ms | 0.0787 ms |  3.86 |    0.11 |    3 | 1557.18 KB |        4.90 |
| Needlr_SourceGen_BuildHost         | 2.934 ms | 0.2985 ms | 0.0775 ms |  1.33 |    0.05 |    2 |  584.23 KB |        1.84 |
| Needlr_SourceGenExplicit_BuildHost | 2.851 ms | 0.3406 ms | 0.0885 ms |  1.30 |    0.05 |    2 |  507.17 KB |        1.59 |
