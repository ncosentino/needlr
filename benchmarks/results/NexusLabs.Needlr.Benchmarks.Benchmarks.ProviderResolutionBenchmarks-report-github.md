```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                              | Mean       | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|------------------------------------ |-----------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
| ManualDI_ServiceProvider_GetService | 15.0468 ns | 0.0943 ns | 0.0245 ns |  1.00 |    0.00 |    2 |         - |          NA |
| Needlr_Reflection_GetService        |         NA |        NA |        NA |     ? |       ? |    ? |        NA |           ? |
| Needlr_SourceGen_GetService         | 15.0253 ns | 0.0987 ns | 0.0256 ns |  1.00 |    0.00 |    2 |         - |          NA |
| Provider_Interface_PropertyAccess   |  0.8946 ns | 0.0117 ns | 0.0030 ns |  0.06 |    0.00 |    1 |         - |          NA |
| Provider_Shorthand_PropertyAccess   |  0.8952 ns | 0.0262 ns | 0.0041 ns |  0.06 |    0.00 |    1 |         - |          NA |

Benchmarks with issues:
  ProviderResolutionBenchmarks.Needlr_Reflection_GetService: ShortRun(IterationCount=5, LaunchCount=1, WarmupCount=3)
