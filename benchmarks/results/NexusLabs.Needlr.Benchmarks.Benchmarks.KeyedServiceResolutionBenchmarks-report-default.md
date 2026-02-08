
BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                         | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
------------------------------- |---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
 ManualDI_ResolveKeyed          | 22.37 ns | 0.050 ns | 0.008 ns |  1.00 |    0.00 |    1 |         - |          NA |
 Needlr_Reflection_ResolveKeyed |       NA |       NA |       NA |     ? |       ? |    ? |        NA |           ? |
 Needlr_SourceGen_ResolveKeyed  | 22.46 ns | 0.057 ns | 0.009 ns |  1.00 |    0.00 |    1 |         - |          NA |

Benchmarks with issues:
  KeyedServiceResolutionBenchmarks.Needlr_Reflection_ResolveKeyed: ShortRun(IterationCount=5, LaunchCount=1, WarmupCount=3)
