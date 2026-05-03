
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 Method                               | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated  | Alloc Ratio |
------------------------------------- |---------:|----------:|----------:|------:|--------:|-----:|-----------:|------------:|
 ManualDI_BuildWebApp                 | 2.782 ms | 0.0183 ms | 0.0028 ms |  1.00 |    0.00 |    1 |  518.63 KB |        1.00 |
 Needlr_Reflection_BuildWebApp        | 8.887 ms | 0.3800 ms | 0.0588 ms |  3.19 |    0.02 |    2 | 1775.97 KB |        3.42 |
 Needlr_SourceGen_BuildWebApp         | 3.522 ms | 0.1608 ms | 0.0418 ms |  1.27 |    0.01 |    1 |  811.58 KB |        1.56 |
 Needlr_SourceGenExplicit_BuildWebApp | 3.458 ms | 0.1144 ms | 0.0297 ms |  1.24 |    0.01 |    1 |  733.52 KB |        1.41 |
