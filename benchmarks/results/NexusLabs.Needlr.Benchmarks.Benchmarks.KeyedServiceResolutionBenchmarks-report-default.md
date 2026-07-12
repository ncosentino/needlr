
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                         | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
------------------------------- |---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
 ManualDI_ResolveKeyed          | 21.89 ns | 0.081 ns | 0.012 ns |  1.00 |    0.00 |    1 |         - |          NA |
 Needlr_Reflection_ResolveKeyed |       NA |       NA |       NA |     ? |       ? |    ? |        NA |           ? |
 Needlr_SourceGen_ResolveKeyed  | 22.70 ns | 0.086 ns | 0.013 ns |  1.04 |    0.00 |    1 |         - |          NA |

Benchmarks with issues:
  KeyedServiceResolutionBenchmarks.Needlr_Reflection_ResolveKeyed: ShortRun(IterationCount=5, LaunchCount=1, WarmupCount=3)
