
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]   : .NET 10.0.8 (10.0.826.23019), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.8 (10.0.826.23019), X64 RyuJIT AVX2

Job=ShortRun  InvocationCount=1  IterationCount=5  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

 Method                             | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated  | Alloc Ratio |
----------------------------------- |---------:|----------:|----------:|------:|--------:|-----:|-----------:|------------:|
 ManualDI_BuildHost                 | 2.342 ms | 0.1735 ms | 0.0268 ms |  1.00 |    0.01 |    1 |  316.91 KB |        1.00 |
 Needlr_Reflection_BuildHost        | 9.001 ms | 0.1879 ms | 0.0488 ms |  3.84 |    0.04 |    3 | 1556.63 KB |        4.91 |
 Needlr_SourceGen_BuildHost         | 3.280 ms | 0.3124 ms | 0.0811 ms |  1.40 |    0.03 |    2 |   583.4 KB |        1.84 |
 Needlr_SourceGenExplicit_BuildHost | 3.100 ms | 0.3019 ms | 0.0784 ms |  1.32 |    0.03 |    2 |  506.63 KB |        1.60 |
