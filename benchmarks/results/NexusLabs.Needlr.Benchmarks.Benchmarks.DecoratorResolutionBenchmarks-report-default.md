
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.202
  [Host]   : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                             | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
----------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
 ManualDI_ResolveDecorated          | 14.62 ns | 0.028 ns | 0.004 ns |  1.00 |    1 |         - |          NA |
 Needlr_Reflection_ResolveDecorated | 20.57 ns | 0.058 ns | 0.015 ns |  1.41 |    2 |         - |          NA |
 Needlr_SourceGen_ResolveDecorated  | 19.11 ns | 0.285 ns | 0.074 ns |  1.31 |    2 |         - |          NA |
