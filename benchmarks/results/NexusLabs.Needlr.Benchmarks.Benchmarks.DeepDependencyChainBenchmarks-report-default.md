
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                             | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
----------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
 ManualDI_ResolveDeepChain          | 15.25 ns | 0.070 ns | 0.018 ns |  1.00 |    1 |         - |          NA |
 Needlr_Reflection_ResolveDeepChain | 15.55 ns | 0.720 ns | 0.187 ns |  1.02 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveDeepChain  | 15.29 ns | 0.211 ns | 0.033 ns |  1.00 |    1 |         - |          NA |
