
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                             | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
----------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
 ManualDI_ResolveDecorated          | 14.35 ns | 0.090 ns | 0.014 ns |  1.00 |    1 |         - |          NA |
 Needlr_Reflection_ResolveDecorated | 18.91 ns | 0.290 ns | 0.075 ns |  1.32 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveDecorated  | 16.41 ns | 0.116 ns | 0.030 ns |  1.14 |    1 |         - |          NA |
