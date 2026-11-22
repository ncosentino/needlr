
BenchmarkDotNet v0.14.0, Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.404
  [Host]   : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  ShortRun : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 Method                               | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated  | Alloc Ratio |
------------------------------------- |---------:|----------:|----------:|------:|--------:|-----:|-----------:|------------:|
 ManualDI_BuildWebApp                 | 2.446 ms | 0.1828 ms | 0.0475 ms |  1.00 |    0.03 |    1 |  519.01 KB |        1.00 |
 Needlr_Reflection_BuildWebApp        | 7.091 ms | 0.3185 ms | 0.0493 ms |  2.90 |    0.05 |    2 | 1767.34 KB |        3.41 |
 Needlr_SourceGen_BuildWebApp         | 2.896 ms | 0.1949 ms | 0.0506 ms |  1.18 |    0.03 |    1 |  800.48 KB |        1.54 |
 Needlr_SourceGenExplicit_BuildWebApp | 2.821 ms | 0.2341 ms | 0.0362 ms |  1.15 |    0.02 |    1 |  723.45 KB |        1.39 |
