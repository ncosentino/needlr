```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
Intel Core 7 150U, 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

```
| Method                             | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated  | Alloc Ratio |
|----------------------------------- |---------:|----------:|----------:|------:|--------:|-----:|-----------:|------------:|
| ManualDI_BuildHost                 | 3.527 ms | 2.1519 ms | 0.3330 ms |  1.01 |    0.13 |    2 |  312.01 KB |        1.00 |
| Needlr_Reflection_BuildHost        | 7.334 ms | 0.6994 ms | 0.1816 ms |  2.09 |    0.20 |    3 | 1541.95 KB |        4.94 |
| Needlr_SourceGen_BuildHost         | 2.470 ms | 0.4010 ms | 0.1041 ms |  0.71 |    0.07 |    1 |   581.8 KB |        1.86 |
| Needlr_SourceGenExplicit_BuildHost | 2.583 ms | 1.5956 ms | 0.2469 ms |  0.74 |    0.09 |    1 |  483.88 KB |        1.55 |
