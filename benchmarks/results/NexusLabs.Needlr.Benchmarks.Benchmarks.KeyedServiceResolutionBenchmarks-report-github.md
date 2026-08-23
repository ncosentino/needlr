```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                         | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|------------------------------- |---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
| ManualDI_ResolveKeyed          | 22.69 ns | 0.019 ns | 0.003 ns |  1.00 |    0.00 |    1 |         - |          NA |
| Needlr_Reflection_ResolveKeyed |       NA |       NA |       NA |     ? |       ? |    ? |        NA |           ? |
| Needlr_SourceGen_ResolveKeyed  | 22.75 ns | 0.079 ns | 0.020 ns |  1.00 |    0.00 |    1 |         - |          NA |

Benchmarks with issues:
  KeyedServiceResolutionBenchmarks.Needlr_Reflection_ResolveKeyed: ShortRun(IterationCount=5, LaunchCount=1, WarmupCount=3)
