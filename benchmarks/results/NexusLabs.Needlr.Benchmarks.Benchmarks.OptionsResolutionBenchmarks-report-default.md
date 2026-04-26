
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                           | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
--------------------------------- |---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
 ManualDI_ResolveOptions          | 16.83 ns | 1.155 ns | 0.300 ns |  1.00 |    0.02 |    1 |         - |          NA |
 Needlr_Reflection_ResolveOptions | 17.77 ns | 0.083 ns | 0.022 ns |  1.06 |    0.02 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveOptions  | 17.87 ns | 0.111 ns | 0.017 ns |  1.06 |    0.02 |    1 |         - |          NA |
