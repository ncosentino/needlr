
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                             | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
----------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
 ManualDI_ResolveDeepChain          | 15.25 ns | 0.172 ns | 0.045 ns |  1.00 |    1 |         - |          NA |
 Needlr_Reflection_ResolveDeepChain | 15.18 ns | 0.056 ns | 0.009 ns |  0.99 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveDeepChain  | 14.59 ns | 0.134 ns | 0.035 ns |  0.96 |    1 |         - |          NA |
