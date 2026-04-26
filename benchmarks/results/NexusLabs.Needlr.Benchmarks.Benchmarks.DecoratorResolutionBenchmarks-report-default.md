
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                             | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
----------------------------------- |---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
 ManualDI_ResolveDecorated          | 14.65 ns | 0.097 ns | 0.015 ns |  1.00 |    0.00 |    1 |         - |          NA |
 Needlr_Reflection_ResolveDecorated | 15.60 ns | 1.096 ns | 0.285 ns |  1.06 |    0.02 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveDecorated  | 15.02 ns | 0.014 ns | 0.002 ns |  1.03 |    0.00 |    1 |         - |          NA |
