
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
12th Gen Intel Core i7-12700H, 1 CPU, 20 logical and 10 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                              | Mean       | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
------------------------------------ |-----------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
 ManualDI_ServiceProvider_GetService | 10.6934 ns | 2.5031 ns | 0.6501 ns |  1.00 |    0.08 |    3 |         - |          NA |
 Needlr_Reflection_GetService        |         NA |        NA |        NA |     ? |       ? |    ? |        NA |           ? |
 Needlr_SourceGen_GetService         | 10.8805 ns | 2.7863 ns | 0.7236 ns |  1.02 |    0.09 |    3 |         - |          NA |
 Provider_Interface_PropertyAccess   |  0.5192 ns | 0.4939 ns | 0.0764 ns |  0.05 |    0.01 |    1 |         - |          NA |
 Provider_Shorthand_PropertyAccess   |  0.6636 ns | 0.2672 ns | 0.0694 ns |  0.06 |    0.01 |    2 |         - |          NA |

Benchmarks with issues:
  ProviderResolutionBenchmarks.Needlr_Reflection_GetService: ShortRun(IterationCount=5, LaunchCount=1, WarmupCount=3)
