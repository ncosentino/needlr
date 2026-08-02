
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
12th Gen Intel Core i7-12700H, 1 CPU, 20 logical and 10 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                             | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
----------------------------------- |---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
 ManualDI_ResolveDeepChain          | 10.89 ns | 1.738 ns | 0.269 ns |  1.00 |    0.03 |    1 |         - |          NA |
 Needlr_Reflection_ResolveDeepChain | 11.11 ns | 1.077 ns | 0.167 ns |  1.02 |    0.03 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveDeepChain  | 11.49 ns | 1.008 ns | 0.156 ns |  1.05 |    0.03 |    1 |         - |          NA |
