
BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                             | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
----------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
 ManualDI_ResolveDeepChain          | 14.43 ns | 0.035 ns | 0.005 ns |  1.00 |    1 |         - |          NA |
 Needlr_Reflection_ResolveDeepChain | 15.53 ns | 0.056 ns | 0.014 ns |  1.08 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveDeepChain  | 14.35 ns | 0.060 ns | 0.009 ns |  0.99 |    1 |         - |          NA |
