
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 Method                               | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
------------------------------------- |---------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
 ManualDI_BuildWebApp                 | 2.875 ms | 0.3314 ms | 0.0861 ms |  1.00 |    0.04 |    1 | 521.65 KB |        1.00 |
 Needlr_Reflection_BuildWebApp        | 9.526 ms | 0.2772 ms | 0.0429 ms |  3.32 |    0.09 |    3 |   1779 KB |        3.41 |
 Needlr_SourceGen_BuildWebApp         | 3.737 ms | 0.2167 ms | 0.0563 ms |  1.30 |    0.04 |    2 | 803.13 KB |        1.54 |
 Needlr_SourceGenExplicit_BuildWebApp | 3.818 ms | 0.2053 ms | 0.0318 ms |  1.33 |    0.04 |    2 | 726.09 KB |        1.39 |
