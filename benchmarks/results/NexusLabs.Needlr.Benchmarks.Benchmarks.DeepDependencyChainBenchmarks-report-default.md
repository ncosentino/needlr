
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                             | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
----------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
 ManualDI_ResolveDeepChain          | 15.25 ns | 0.052 ns | 0.008 ns |  1.00 |    1 |         - |          NA |
 Needlr_Reflection_ResolveDeepChain | 15.33 ns | 0.011 ns | 0.003 ns |  1.01 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveDeepChain  | 15.40 ns | 0.603 ns | 0.157 ns |  1.01 |    1 |         - |          NA |
