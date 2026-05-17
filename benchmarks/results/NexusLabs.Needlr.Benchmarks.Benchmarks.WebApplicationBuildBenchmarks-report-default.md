
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]   : .NET 10.0.8 (10.0.826.23019), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.8 (10.0.826.23019), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 Method                               | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated  | Alloc Ratio |
------------------------------------- |---------:|----------:|----------:|------:|--------:|-----:|-----------:|------------:|
 ManualDI_BuildWebApp                 | 3.563 ms | 0.2432 ms | 0.0632 ms |  1.00 |    0.02 |    1 |  518.63 KB |        1.00 |
 Needlr_Reflection_BuildWebApp        | 9.423 ms | 0.4832 ms | 0.0748 ms |  2.65 |    0.05 |    2 | 1775.97 KB |        3.42 |
 Needlr_SourceGen_BuildWebApp         | 3.934 ms | 0.2903 ms | 0.0754 ms |  1.10 |    0.03 |    1 |  811.58 KB |        1.56 |
 Needlr_SourceGenExplicit_BuildWebApp | 4.026 ms | 0.5510 ms | 0.0853 ms |  1.13 |    0.03 |    1 |  732.86 KB |        1.41 |
