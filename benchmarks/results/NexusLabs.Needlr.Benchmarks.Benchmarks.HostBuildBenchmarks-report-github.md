```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.404
  [Host]   : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  ShortRun : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

```
| Method                             | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated  | Alloc Ratio |
|----------------------------------- |---------:|----------:|----------:|------:|--------:|-----:|-----------:|------------:|
| ManualDI_BuildHost                 | 1.658 ms | 0.1870 ms | 0.0289 ms |  1.00 |    0.02 |    1 |  318.05 KB |        1.00 |
| Needlr_Reflection_BuildHost        | 6.197 ms | 0.2829 ms | 0.0735 ms |  3.74 |    0.07 |    3 | 1603.48 KB |        5.04 |
| Needlr_SourceGen_BuildHost         | 2.205 ms | 0.1026 ms | 0.0159 ms |  1.33 |    0.02 |    2 |  623.48 KB |        1.96 |
| Needlr_SourceGenExplicit_BuildHost | 2.167 ms | 0.1228 ms | 0.0190 ms |  1.31 |    0.02 |    2 |   551.2 KB |        1.73 |
