
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 Method                               | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated  | Alloc Ratio |
------------------------------------- |---------:|----------:|----------:|------:|--------:|-----:|-----------:|------------:|
 ManualDI_BuildWebApp                 | 2.734 ms | 0.1518 ms | 0.0394 ms |  1.00 |    0.02 |    1 |  518.63 KB |        1.00 |
 Needlr_Reflection_BuildWebApp        | 9.813 ms | 0.7013 ms | 0.1821 ms |  3.59 |    0.08 |    3 | 1775.97 KB |        3.42 |
 Needlr_SourceGen_BuildWebApp         | 3.809 ms | 0.3670 ms | 0.0953 ms |  1.39 |    0.04 |    2 |  811.58 KB |        1.56 |
 Needlr_SourceGenExplicit_BuildWebApp | 3.471 ms | 0.0849 ms | 0.0131 ms |  1.27 |    0.02 |    2 |  733.52 KB |        1.41 |
