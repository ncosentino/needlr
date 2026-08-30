
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 Method                             | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated  | Alloc Ratio |
----------------------------------- |---------:|----------:|----------:|------:|--------:|-----:|-----------:|------------:|
 ManualDI_BuildHost                 | 3.524 ms | 0.5362 ms | 0.0830 ms |  1.00 |    0.03 |    1 |  318.95 KB |        1.00 |
 Needlr_Reflection_BuildHost        | 9.318 ms | 0.5189 ms | 0.1348 ms |  2.64 |    0.07 |    2 | 1604.47 KB |        5.03 |
 Needlr_SourceGen_BuildHost         | 3.201 ms | 0.2833 ms | 0.0736 ms |  0.91 |    0.03 |    1 |   624.6 KB |        1.96 |
 Needlr_SourceGenExplicit_BuildHost | 2.971 ms | 0.3927 ms | 0.0608 ms |  0.84 |    0.02 |    1 |  552.31 KB |        1.73 |
