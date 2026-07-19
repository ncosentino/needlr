
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                             | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
----------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
 ManualDI_ResolveDependent          | 15.64 ns | 0.062 ns | 0.010 ns |  1.00 |    1 |         - |          NA |
 Needlr_Reflection_ResolveDependent | 15.76 ns | 0.141 ns | 0.022 ns |  1.01 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveDependent  | 19.94 ns | 0.026 ns | 0.004 ns |  1.27 |    2 |         - |          NA |
