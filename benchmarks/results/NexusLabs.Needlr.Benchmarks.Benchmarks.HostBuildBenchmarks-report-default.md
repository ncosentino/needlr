
BenchmarkDotNet v0.14.0, Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.303
  [Host]   : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  ShortRun : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 Method                             | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated  | Alloc Ratio |
----------------------------------- |---------:|----------:|----------:|------:|--------:|-----:|-----------:|------------:|
 ManualDI_BuildHost                 | 1.689 ms | 0.0555 ms | 0.0086 ms |  1.00 |    0.01 |    1 |  318.05 KB |        1.00 |
 Needlr_Reflection_BuildHost        | 6.176 ms | 0.1838 ms | 0.0284 ms |  3.66 |    0.02 |    3 | 1603.48 KB |        5.04 |
 Needlr_SourceGen_BuildHost         | 2.248 ms | 0.1409 ms | 0.0366 ms |  1.33 |    0.02 |    2 |  623.48 KB |        1.96 |
 Needlr_SourceGenExplicit_BuildHost | 2.211 ms | 0.0882 ms | 0.0229 ms |  1.31 |    0.01 |    2 |  550.91 KB |        1.73 |
