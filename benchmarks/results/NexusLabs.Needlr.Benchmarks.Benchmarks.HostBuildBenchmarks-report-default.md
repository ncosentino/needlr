
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 Method                             | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated  | Alloc Ratio |
----------------------------------- |---------:|----------:|----------:|------:|--------:|-----:|-----------:|------------:|
 ManualDI_BuildHost                 | 2.482 ms | 1.3386 ms | 0.3476 ms |  1.02 |    0.18 |    1 |  318.14 KB |        1.00 |
 Needlr_Reflection_BuildHost        | 8.192 ms | 0.2113 ms | 0.0549 ms |  3.35 |    0.42 |    2 | 1557.34 KB |        4.90 |
 Needlr_SourceGen_BuildHost         | 2.805 ms | 0.1102 ms | 0.0286 ms |  1.15 |    0.14 |    1 |  584.11 KB |        1.84 |
 Needlr_SourceGenExplicit_BuildHost | 2.740 ms | 0.1219 ms | 0.0317 ms |  1.12 |    0.14 |    1 |  507.34 KB |        1.59 |
