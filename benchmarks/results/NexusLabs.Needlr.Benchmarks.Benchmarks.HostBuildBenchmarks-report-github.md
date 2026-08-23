```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

```
| Method                             | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated  | Alloc Ratio |
|----------------------------------- |---------:|----------:|----------:|------:|--------:|-----:|-----------:|------------:|
| ManualDI_BuildHost                 | 2.187 ms | 0.2802 ms | 0.0434 ms |  1.00 |    0.02 |    1 |  318.95 KB |        1.00 |
| Needlr_Reflection_BuildHost        | 8.256 ms | 0.2946 ms | 0.0765 ms |  3.78 |    0.07 |    3 | 1604.47 KB |        5.03 |
| Needlr_SourceGen_BuildHost         | 2.909 ms | 0.1626 ms | 0.0422 ms |  1.33 |    0.03 |    2 |  624.88 KB |        1.96 |
| Needlr_SourceGenExplicit_BuildHost | 2.877 ms | 0.1442 ms | 0.0375 ms |  1.32 |    0.03 |    2 |  552.59 KB |        1.73 |
