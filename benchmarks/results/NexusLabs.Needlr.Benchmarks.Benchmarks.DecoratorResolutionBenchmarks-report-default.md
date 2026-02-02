
BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                             | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
----------------------------------- |---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
 ManualDI_ResolveDecorated          | 15.32 ns | 0.060 ns | 0.009 ns |  1.00 |    0.00 |    1 |         - |          NA |
 Needlr_Reflection_ResolveDecorated | 15.30 ns | 1.199 ns | 0.311 ns |  1.00 |    0.02 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveDecorated  | 15.51 ns | 2.898 ns | 0.753 ns |  1.01 |    0.05 |    1 |         - |          NA |
