
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
12th Gen Intel Core i7-12700H, 1 CPU, 20 logical and 10 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 Method                               | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated  | Alloc Ratio |
------------------------------------- |---------:|----------:|----------:|------:|--------:|-----:|-----------:|------------:|
 ManualDI_BuildWebApp                 | 2.699 ms | 0.6068 ms | 0.0939 ms |  1.00 |    0.04 |    1 |   542.6 KB |        1.00 |
 Needlr_Reflection_BuildWebApp        | 7.271 ms | 2.0746 ms | 0.5388 ms |  2.70 |    0.20 |    2 | 1791.62 KB |        3.30 |
 Needlr_SourceGen_BuildWebApp         | 3.233 ms | 0.1877 ms | 0.0290 ms |  1.20 |    0.04 |    1 |  799.66 KB |        1.47 |
 Needlr_SourceGenExplicit_BuildWebApp | 3.079 ms | 0.6110 ms | 0.1587 ms |  1.14 |    0.06 |    1 |  720.94 KB |        1.33 |
