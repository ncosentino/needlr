
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.202
  [Host]   : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 Method                               | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated  | Alloc Ratio |
------------------------------------- |---------:|----------:|----------:|------:|--------:|-----:|-----------:|------------:|
 ManualDI_BuildWebApp                 | 2.700 ms | 0.0747 ms | 0.0194 ms |  1.00 |    0.01 |    1 |  518.63 KB |        1.00 |
 Needlr_Reflection_BuildWebApp        | 8.552 ms | 0.1209 ms | 0.0314 ms |  3.17 |    0.02 |    3 | 1774.98 KB |        3.42 |
 Needlr_SourceGen_BuildWebApp         | 3.494 ms | 0.1401 ms | 0.0364 ms |  1.29 |    0.01 |    2 |  811.58 KB |        1.56 |
 Needlr_SourceGenExplicit_BuildWebApp | 3.448 ms | 0.1637 ms | 0.0425 ms |  1.28 |    0.02 |    2 |  733.52 KB |        1.41 |
