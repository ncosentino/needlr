
BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                             | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
----------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
 ManualDI_ResolveDependent          | 14.94 ns | 0.348 ns | 0.090 ns |  1.00 |    1 |         - |          NA |
 Needlr_Reflection_ResolveDependent | 14.85 ns | 0.847 ns | 0.220 ns |  0.99 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveDependent  | 14.45 ns | 0.370 ns | 0.096 ns |  0.97 |    1 |         - |          NA |
