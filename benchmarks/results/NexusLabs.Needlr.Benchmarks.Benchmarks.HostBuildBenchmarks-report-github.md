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
| ManualDI_BuildHost                 | 2.110 ms | 0.0921 ms | 0.0143 ms |  1.00 |    0.01 |    1 |  318.14 KB |        1.00 |
| Needlr_Reflection_BuildHost        | 8.202 ms | 0.2013 ms | 0.0523 ms |  3.89 |    0.03 |    3 | 1557.34 KB |        4.90 |
| Needlr_SourceGen_BuildHost         | 2.863 ms | 0.1002 ms | 0.0155 ms |  1.36 |    0.01 |    2 |  584.39 KB |        1.84 |
| Needlr_SourceGenExplicit_BuildHost | 2.755 ms | 0.0795 ms | 0.0206 ms |  1.31 |    0.01 |    2 |  507.34 KB |        1.59 |
