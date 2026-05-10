
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                              | Mean       | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
------------------------------------ |-----------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
 ManualDI_ServiceProvider_GetService | 19.3068 ns | 1.0005 ns | 0.1548 ns |  1.00 |    0.01 |    2 |         - |          NA |
 Needlr_Reflection_GetService        |         NA |        NA |        NA |     ? |       ? |    ? |        NA |           ? |
 Needlr_SourceGen_GetService         | 18.9166 ns | 0.7991 ns | 0.1237 ns |  0.98 |    0.01 |    2 |         - |          NA |
 Provider_Interface_PropertyAccess   |  0.5504 ns | 0.5849 ns | 0.1519 ns |  0.03 |    0.01 |    1 |         - |          NA |
 Provider_Shorthand_PropertyAccess   |  0.5430 ns | 0.6002 ns | 0.1559 ns |  0.03 |    0.01 |    1 |         - |          NA |

Benchmarks with issues:
  ProviderResolutionBenchmarks.Needlr_Reflection_GetService: ShortRun(IterationCount=5, LaunchCount=1, WarmupCount=3)
