```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                              | Mean       | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|------------------------------------ |-----------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
| ManualDI_ServiceProvider_GetService | 16.5752 ns | 0.1847 ns | 0.0286 ns |  1.00 |    0.00 |    2 |         - |          NA |
| Needlr_Reflection_GetService        |         NA |        NA |        NA |     ? |       ? |    ? |        NA |           ? |
| Needlr_SourceGen_GetService         | 15.5448 ns | 0.2954 ns | 0.0457 ns |  0.94 |    0.00 |    2 |         - |          NA |
| Provider_Interface_PropertyAccess   |  0.9917 ns | 0.0153 ns | 0.0024 ns |  0.06 |    0.00 |    1 |         - |          NA |
| Provider_Shorthand_PropertyAccess   |  1.0229 ns | 0.0051 ns | 0.0008 ns |  0.06 |    0.00 |    1 |         - |          NA |

Benchmarks with issues:
  ProviderResolutionBenchmarks.Needlr_Reflection_GetService: ShortRun(IterationCount=5, LaunchCount=1, WarmupCount=3)
