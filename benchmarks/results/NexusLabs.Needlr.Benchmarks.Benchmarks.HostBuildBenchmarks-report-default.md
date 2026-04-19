
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.202
  [Host]   : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 Method                             | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated  | Alloc Ratio |
----------------------------------- |---------:|----------:|----------:|------:|--------:|-----:|-----------:|------------:|
 ManualDI_BuildHost                 | 3.153 ms | 0.2169 ms | 0.0563 ms |  1.00 |    0.02 |    1 |  317.28 KB |        1.00 |
 Needlr_Reflection_BuildHost        | 8.045 ms | 0.5838 ms | 0.0904 ms |  2.55 |    0.05 |    2 | 1556.63 KB |        4.91 |
 Needlr_SourceGen_BuildHost         | 2.925 ms | 0.1016 ms | 0.0157 ms |  0.93 |    0.02 |    1 |   583.4 KB |        1.84 |
 Needlr_SourceGenExplicit_BuildHost | 2.872 ms | 0.0857 ms | 0.0223 ms |  0.91 |    0.02 |    1 |  506.34 KB |        1.60 |
