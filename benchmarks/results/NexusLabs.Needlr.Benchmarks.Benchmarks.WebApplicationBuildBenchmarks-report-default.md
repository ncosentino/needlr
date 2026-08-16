
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
Intel Core 7 150U, 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 Method                               | Mean      | Error      | StdDev    | Ratio | RatioSD | Rank | Allocated  | Alloc Ratio |
------------------------------------- |----------:|-----------:|----------:|------:|--------:|-----:|-----------:|------------:|
 ManualDI_BuildWebApp                 |  2.401 ms |  0.5665 ms | 0.1471 ms |  1.00 |    0.08 |    1 |   506.9 KB |        1.00 |
 Needlr_Reflection_BuildWebApp        | 16.734 ms | 12.4252 ms | 3.2268 ms |  6.99 |    1.29 |    4 | 1747.58 KB |        3.45 |
 Needlr_SourceGen_BuildWebApp         |  3.314 ms |  0.5256 ms | 0.1365 ms |  1.38 |    0.09 |    2 |  811.63 KB |        1.60 |
 Needlr_SourceGenExplicit_BuildWebApp |  5.686 ms |  1.6743 ms | 0.4348 ms |  2.38 |    0.21 |    3 |  716.42 KB |        1.41 |
