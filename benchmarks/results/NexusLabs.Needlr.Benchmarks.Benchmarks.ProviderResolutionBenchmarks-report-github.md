```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                              | Mean       | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|------------------------------------ |-----------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
| ManualDI_ServiceProvider_GetService | 19.7406 ns | 0.2256 ns | 0.0586 ns |  1.00 |    0.00 |    2 |         - |          NA |
| Needlr_Reflection_GetService        |         NA |        NA |        NA |     ? |       ? |    ? |        NA |           ? |
| Needlr_SourceGen_GetService         | 20.0529 ns | 1.3573 ns | 0.3525 ns |  1.02 |    0.02 |    2 |         - |          NA |
| Provider_Interface_PropertyAccess   |  0.6892 ns | 0.4333 ns | 0.1125 ns |  0.03 |    0.01 |    1 |         - |          NA |
| Provider_Shorthand_PropertyAccess   |  0.8522 ns | 0.0284 ns | 0.0074 ns |  0.04 |    0.00 |    1 |         - |          NA |

Benchmarks with issues:
  ProviderResolutionBenchmarks.Needlr_Reflection_GetService: ShortRun(IterationCount=5, LaunchCount=1, WarmupCount=3)
