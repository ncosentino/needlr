```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                              | Mean       | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|------------------------------------ |-----------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
| ManualDI_ServiceProvider_GetService | 14.8328 ns | 0.8169 ns | 0.2121 ns |  1.00 |    0.02 |    2 |         - |          NA |
| Needlr_Reflection_GetService        |         NA |        NA |        NA |     ? |       ? |    ? |        NA |           ? |
| Needlr_SourceGen_GetService         | 14.8458 ns | 1.2111 ns | 0.3145 ns |  1.00 |    0.02 |    2 |         - |          NA |
| Provider_Interface_PropertyAccess   |  0.8941 ns | 0.0200 ns | 0.0031 ns |  0.06 |    0.00 |    1 |         - |          NA |
| Provider_Shorthand_PropertyAccess   |  0.8984 ns | 0.0147 ns | 0.0038 ns |  0.06 |    0.00 |    1 |         - |          NA |

Benchmarks with issues:
  ProviderResolutionBenchmarks.Needlr_Reflection_GetService: ShortRun(IterationCount=5, LaunchCount=1, WarmupCount=3)
