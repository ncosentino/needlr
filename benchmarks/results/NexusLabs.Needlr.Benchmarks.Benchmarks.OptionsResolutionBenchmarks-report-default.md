
BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                           | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
--------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
 ManualDI_ResolveOptions          | 16.97 ns | 0.093 ns | 0.014 ns |  1.00 |    1 |         - |          NA |
 Needlr_Reflection_ResolveOptions | 16.93 ns | 0.088 ns | 0.023 ns |  1.00 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveOptions  | 17.83 ns | 0.175 ns | 0.027 ns |  1.05 |    1 |         - |          NA |
