
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                             | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
----------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
 ManualDI_ResolveDecorated          | 15.91 ns | 0.681 ns | 0.177 ns |  1.00 |    1 |         - |          NA |
 Needlr_Reflection_ResolveDecorated | 15.11 ns | 0.093 ns | 0.014 ns |  0.95 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveDecorated  | 15.06 ns | 0.206 ns | 0.032 ns |  0.95 |    1 |         - |          NA |
