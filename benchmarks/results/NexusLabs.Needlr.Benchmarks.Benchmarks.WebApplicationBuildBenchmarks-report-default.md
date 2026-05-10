
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 Method                               | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated  | Alloc Ratio |
------------------------------------- |---------:|----------:|----------:|------:|--------:|-----:|-----------:|------------:|
 ManualDI_BuildWebApp                 | 2.820 ms | 0.1807 ms | 0.0469 ms |  1.00 |    0.02 |    1 |  518.63 KB |        1.00 |
 Needlr_Reflection_BuildWebApp        | 8.522 ms | 0.1579 ms | 0.0410 ms |  3.02 |    0.05 |    2 | 1774.98 KB |        3.42 |
 Needlr_SourceGen_BuildWebApp         | 3.532 ms | 0.2072 ms | 0.0538 ms |  1.25 |    0.03 |    1 |  811.58 KB |        1.56 |
 Needlr_SourceGenExplicit_BuildWebApp | 3.446 ms | 0.2277 ms | 0.0591 ms |  1.22 |    0.03 |    1 |  733.52 KB |        1.41 |
