
BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 Method                               | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated  | Alloc Ratio |
------------------------------------- |---------:|----------:|----------:|------:|--------:|-----:|-----------:|------------:|
 ManualDI_BuildWebApp                 | 2.695 ms | 0.1941 ms | 0.0504 ms |  1.00 |    0.02 |    1 |  517.07 KB |        1.00 |
 Needlr_Reflection_BuildWebApp        | 8.957 ms | 0.1410 ms | 0.0366 ms |  3.32 |    0.06 |    2 | 1746.74 KB |        3.38 |
 Needlr_SourceGen_BuildWebApp         | 3.367 ms | 0.2353 ms | 0.0611 ms |  1.25 |    0.03 |    1 |  734.78 KB |        1.42 |
 Needlr_SourceGenExplicit_BuildWebApp | 3.294 ms | 0.0295 ms | 0.0077 ms |  1.22 |    0.02 |    1 |  674.95 KB |        1.31 |
