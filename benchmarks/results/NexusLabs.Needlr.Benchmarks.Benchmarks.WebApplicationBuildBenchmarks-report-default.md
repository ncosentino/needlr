
BenchmarkDotNet v0.14.0, Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.404
  [Host]   : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 Method                               | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated  | Alloc Ratio |
------------------------------------- |---------:|----------:|----------:|------:|--------:|-----:|-----------:|------------:|
 ManualDI_BuildWebApp                 | 2.874 ms | 0.1631 ms | 0.0252 ms |  1.00 |    0.01 |    1 |  519.01 KB |        1.00 |
 Needlr_Reflection_BuildWebApp        | 9.717 ms | 2.5014 ms | 0.6496 ms |  3.38 |    0.21 |    3 | 1767.34 KB |        3.41 |
 Needlr_SourceGen_BuildWebApp         | 3.764 ms | 0.2049 ms | 0.0317 ms |  1.31 |    0.01 |    2 |  800.48 KB |        1.54 |
 Needlr_SourceGenExplicit_BuildWebApp | 3.939 ms | 0.3892 ms | 0.1011 ms |  1.37 |    0.03 |    2 |  723.45 KB |        1.39 |
