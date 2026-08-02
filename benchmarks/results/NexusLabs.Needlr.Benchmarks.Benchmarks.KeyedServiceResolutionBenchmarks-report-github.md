```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
12th Gen Intel Core i7-12700H, 1 CPU, 20 logical and 10 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                         | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|------------------------------- |---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
| ManualDI_ResolveKeyed          | 14.58 ns | 2.086 ns | 0.542 ns |  1.00 |    0.05 |    1 |         - |          NA |
| Needlr_Reflection_ResolveKeyed |       NA |       NA |       NA |     ? |       ? |    ? |        NA |           ? |
| Needlr_SourceGen_ResolveKeyed  | 16.20 ns | 6.453 ns | 1.676 ns |  1.11 |    0.11 |    1 |         - |          NA |

Benchmarks with issues:
  KeyedServiceResolutionBenchmarks.Needlr_Reflection_ResolveKeyed: ShortRun(IterationCount=5, LaunchCount=1, WarmupCount=3)
