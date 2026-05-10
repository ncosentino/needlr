
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                             | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
----------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
 ManualDI_ResolveDeepChain          | 15.81 ns | 0.082 ns | 0.021 ns |  1.00 |    1 |         - |          NA |
 Needlr_Reflection_ResolveDeepChain | 16.20 ns | 0.051 ns | 0.008 ns |  1.02 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveDeepChain  | 19.26 ns | 0.056 ns | 0.015 ns |  1.22 |    1 |         - |          NA |
