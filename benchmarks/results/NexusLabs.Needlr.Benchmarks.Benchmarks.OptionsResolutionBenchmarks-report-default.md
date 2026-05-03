
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                           | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
--------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
 ManualDI_ResolveOptions          | 16.92 ns | 0.390 ns | 0.060 ns |  1.00 |    1 |         - |          NA |
 Needlr_Reflection_ResolveOptions | 17.53 ns | 1.019 ns | 0.265 ns |  1.04 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveOptions  | 17.79 ns | 0.141 ns | 0.037 ns |  1.05 |    1 |         - |          NA |
