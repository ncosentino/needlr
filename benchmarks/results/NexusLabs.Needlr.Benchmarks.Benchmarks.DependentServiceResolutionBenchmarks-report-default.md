
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                             | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
----------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
 ManualDI_ResolveDependent          | 14.56 ns | 0.149 ns | 0.023 ns |  1.00 |    1 |         - |          NA |
 Needlr_Reflection_ResolveDependent | 15.19 ns | 0.103 ns | 0.027 ns |  1.04 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveDependent  | 15.04 ns | 0.259 ns | 0.040 ns |  1.03 |    1 |         - |          NA |
