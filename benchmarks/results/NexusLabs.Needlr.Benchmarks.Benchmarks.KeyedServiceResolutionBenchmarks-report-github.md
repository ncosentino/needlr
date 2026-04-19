```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.202
  [Host]   : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                         | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|------------------------------- |---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
| ManualDI_ResolveKeyed          | 27.12 ns | 0.068 ns | 0.018 ns |  1.00 |    0.00 |    1 |         - |          NA |
| Needlr_Reflection_ResolveKeyed |       NA |       NA |       NA |     ? |       ? |    ? |        NA |           ? |
| Needlr_SourceGen_ResolveKeyed  | 26.68 ns | 0.057 ns | 0.015 ns |  0.98 |    0.00 |    1 |         - |          NA |

Benchmarks with issues:
  KeyedServiceResolutionBenchmarks.Needlr_Reflection_ResolveKeyed: ShortRun(IterationCount=5, LaunchCount=1, WarmupCount=3)
