
BenchmarkDotNet v0.14.0, Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.404
  [Host]   : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                             | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
----------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
 ManualDI_ResolveDecorated          | 15.27 ns | 0.061 ns | 0.009 ns |  1.00 |    1 |         - |          NA |
 Needlr_Reflection_ResolveDecorated | 15.66 ns | 0.443 ns | 0.115 ns |  1.03 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveDecorated  | 17.94 ns | 0.669 ns | 0.174 ns |  1.17 |    1 |         - |          NA |
