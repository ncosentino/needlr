
BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 Method                             | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated  | Alloc Ratio |
----------------------------------- |---------:|----------:|----------:|------:|--------:|-----:|-----------:|------------:|
 ManualDI_BuildHost                 | 2.107 ms | 0.2069 ms | 0.0320 ms |  1.00 |    0.02 |    1 |  317.58 KB |        1.00 |
 Needlr_Reflection_BuildHost        | 8.417 ms | 0.1311 ms | 0.0340 ms |  4.00 |    0.06 |    3 | 1583.75 KB |        4.99 |
 Needlr_SourceGen_BuildHost         | 2.783 ms | 0.1275 ms | 0.0331 ms |  1.32 |    0.02 |    2 |  566.84 KB |        1.78 |
 Needlr_SourceGenExplicit_BuildHost | 2.706 ms | 0.2126 ms | 0.0552 ms |  1.28 |    0.03 |    2 |  495.49 KB |        1.56 |
