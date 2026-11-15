```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.404
  [Host]   : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

```
| Method                             | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated  | Alloc Ratio |
|----------------------------------- |---------:|----------:|----------:|------:|--------:|-----:|-----------:|------------:|
| ManualDI_BuildHost                 | 2.242 ms | 0.5119 ms | 0.0792 ms |  1.00 |    0.04 |    1 |  318.05 KB |        1.00 |
| Needlr_Reflection_BuildHost        | 8.609 ms | 0.5581 ms | 0.1449 ms |  3.84 |    0.13 |    3 | 1603.48 KB |        5.04 |
| Needlr_SourceGen_BuildHost         | 3.023 ms | 0.3227 ms | 0.0838 ms |  1.35 |    0.05 |    2 |  623.48 KB |        1.96 |
| Needlr_SourceGenExplicit_BuildHost | 3.030 ms | 0.2166 ms | 0.0563 ms |  1.35 |    0.05 |    2 |   551.2 KB |        1.73 |
