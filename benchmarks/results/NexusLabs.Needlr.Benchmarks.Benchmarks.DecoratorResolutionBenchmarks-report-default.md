
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                             | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
----------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
 ManualDI_ResolveDecorated          | 14.63 ns | 0.268 ns | 0.042 ns |  1.00 |    1 |         - |          NA |
 Needlr_Reflection_ResolveDecorated | 14.59 ns | 0.075 ns | 0.012 ns |  1.00 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveDecorated  | 15.15 ns | 0.078 ns | 0.012 ns |  1.04 |    1 |         - |          NA |
