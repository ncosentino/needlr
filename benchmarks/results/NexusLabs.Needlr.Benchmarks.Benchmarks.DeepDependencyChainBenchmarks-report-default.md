
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.202
  [Host]   : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                             | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
----------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
 ManualDI_ResolveDeepChain          | 16.19 ns | 0.096 ns | 0.015 ns |  1.00 |    1 |         - |          NA |
 Needlr_Reflection_ResolveDeepChain | 19.39 ns | 0.512 ns | 0.133 ns |  1.20 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveDeepChain  | 19.43 ns | 0.251 ns | 0.065 ns |  1.20 |    1 |         - |          NA |
