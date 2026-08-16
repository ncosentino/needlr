
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
Intel Core 7 150U, 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                              | Mean       | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
------------------------------------ |-----------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
 ManualDI_ServiceProvider_GetService | 10.2409 ns | 1.4779 ns | 0.2287 ns |  1.00 |    0.03 |    2 |         - |          NA |
 Needlr_Reflection_GetService        |         NA |        NA |        NA |     ? |       ? |    ? |        NA |           ? |
 Needlr_SourceGen_GetService         | 11.1424 ns | 3.0926 ns | 0.4786 ns |  1.09 |    0.05 |    2 |         - |          NA |
 Provider_Interface_PropertyAccess   |  0.5723 ns | 0.7590 ns | 0.1175 ns |  0.06 |    0.01 |    1 |         - |          NA |
 Provider_Shorthand_PropertyAccess   |  0.5337 ns | 0.9461 ns | 0.1464 ns |  0.05 |    0.01 |    1 |         - |          NA |

Benchmarks with issues:
  ProviderResolutionBenchmarks.Needlr_Reflection_GetService: ShortRun(IterationCount=5, LaunchCount=1, WarmupCount=3)
