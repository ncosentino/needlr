
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                          | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
-------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
 ManualDI_ResolveSimple          | 14.55 ns | 0.175 ns | 0.027 ns |  1.00 |    1 |         - |          NA |
 Needlr_Reflection_ResolveSimple | 14.96 ns | 0.665 ns | 0.173 ns |  1.03 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveSimple  | 15.28 ns | 0.077 ns | 0.012 ns |  1.05 |    1 |         - |          NA |
