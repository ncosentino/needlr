
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                             | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
----------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
 ManualDI_ResolveDecorated          | 15.74 ns | 0.028 ns | 0.007 ns |  1.00 |    1 |         - |          NA |
 Needlr_Reflection_ResolveDecorated | 15.02 ns | 0.020 ns | 0.005 ns |  0.95 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveDecorated  | 15.74 ns | 0.725 ns | 0.188 ns |  1.00 |    1 |         - |          NA |
