
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 Method                             | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated  | Alloc Ratio |
----------------------------------- |---------:|----------:|----------:|------:|--------:|-----:|-----------:|------------:|
 ManualDI_BuildHost                 | 2.181 ms | 0.2363 ms | 0.0366 ms |  1.00 |    0.02 |    1 |  317.56 KB |        1.00 |
 Needlr_Reflection_BuildHost        | 8.161 ms | 0.2175 ms | 0.0565 ms |  3.74 |    0.06 |    3 | 1556.63 KB |        4.90 |
 Needlr_SourceGen_BuildHost         | 2.784 ms | 0.0960 ms | 0.0249 ms |  1.28 |    0.02 |    2 |  583.68 KB |        1.84 |
 Needlr_SourceGenExplicit_BuildHost | 2.724 ms | 0.1545 ms | 0.0401 ms |  1.25 |    0.03 |    2 |  506.63 KB |        1.60 |
