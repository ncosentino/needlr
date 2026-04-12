
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.201
  [Host]   : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                             | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
----------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
 ManualDI_ResolveDeepChain          | 15.79 ns | 0.151 ns | 0.023 ns |  1.00 |    1 |         - |          NA |
 Needlr_Reflection_ResolveDeepChain | 15.58 ns | 0.288 ns | 0.075 ns |  0.99 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveDeepChain  | 20.22 ns | 0.092 ns | 0.014 ns |  1.28 |    2 |         - |          NA |
