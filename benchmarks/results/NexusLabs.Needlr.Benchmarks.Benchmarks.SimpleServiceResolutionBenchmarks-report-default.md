
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                          | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
-------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
 ManualDI_ResolveSimple          | 14.94 ns | 0.050 ns | 0.008 ns |  1.00 |    1 |         - |          NA |
 Needlr_Reflection_ResolveSimple | 17.73 ns | 0.031 ns | 0.005 ns |  1.19 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveSimple  | 15.31 ns | 0.269 ns | 0.042 ns |  1.02 |    1 |         - |          NA |
