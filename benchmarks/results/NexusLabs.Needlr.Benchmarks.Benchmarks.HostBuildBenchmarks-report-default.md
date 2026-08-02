
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
12th Gen Intel Core i7-12700H, 1 CPU, 20 logical and 10 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 Method                             | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated  | Alloc Ratio |
----------------------------------- |---------:|----------:|----------:|------:|--------:|-----:|-----------:|------------:|
 ManualDI_BuildHost                 | 2.987 ms | 1.4750 ms | 0.3831 ms |  1.01 |    0.17 |    1 |  326.84 KB |        1.00 |
 Needlr_Reflection_BuildHost        | 7.020 ms | 1.6489 ms | 0.4282 ms |  2.38 |    0.33 |    2 | 1549.44 KB |        4.74 |
 Needlr_SourceGen_BuildHost         | 2.697 ms | 0.4915 ms | 0.1276 ms |  0.92 |    0.12 |    1 |  589.11 KB |        1.80 |
 Needlr_SourceGenExplicit_BuildHost | 2.954 ms | 0.5636 ms | 0.1464 ms |  1.00 |    0.13 |    1 |   500.7 KB |        1.53 |
