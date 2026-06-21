
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 Method                               | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated  | Alloc Ratio |
------------------------------------- |---------:|----------:|----------:|------:|--------:|-----:|-----------:|------------:|
 ManualDI_BuildWebApp                 | 2.827 ms | 0.1802 ms | 0.0468 ms |  1.00 |    0.02 |    1 |  519.61 KB |        1.00 |
 Needlr_Reflection_BuildWebApp        | 9.142 ms | 0.3621 ms | 0.0940 ms |  3.23 |    0.06 |    3 | 1776.95 KB |        3.42 |
 Needlr_SourceGen_BuildWebApp         | 3.565 ms | 0.2627 ms | 0.0682 ms |  1.26 |    0.03 |    2 |  812.27 KB |        1.56 |
 Needlr_SourceGenExplicit_BuildWebApp | 3.529 ms | 0.1580 ms | 0.0410 ms |  1.25 |    0.02 |    2 |  734.49 KB |        1.41 |
