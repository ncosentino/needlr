
BenchmarkDotNet v0.14.0, Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.303
  [Host]   : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  ShortRun : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                             | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
----------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
 ManualDI_ResolveDeepChain          | 12.55 ns | 0.101 ns | 0.016 ns |  1.00 |    1 |         - |          NA |
 Needlr_Reflection_ResolveDeepChain | 16.04 ns | 0.070 ns | 0.011 ns |  1.28 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveDeepChain  | 14.89 ns | 0.017 ns | 0.004 ns |  1.19 |    1 |         - |          NA |
