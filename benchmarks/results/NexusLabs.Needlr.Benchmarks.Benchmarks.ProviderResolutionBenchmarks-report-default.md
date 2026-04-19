
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.202
  [Host]   : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                              | Mean       | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
------------------------------------ |-----------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
 ManualDI_ServiceProvider_GetService | 21.1674 ns | 0.2494 ns | 0.0386 ns |  1.00 |    0.00 |    3 |         - |          NA |
 Needlr_Reflection_GetService        |         NA |        NA |        NA |     ? |       ? |    ? |        NA |           ? |
 Needlr_SourceGen_GetService         | 21.7466 ns | 0.2433 ns | 0.0632 ns |  1.03 |    0.00 |    3 |         - |          NA |
 Provider_Interface_PropertyAccess   |  0.4318 ns | 0.0931 ns | 0.0144 ns |  0.02 |    0.00 |    1 |         - |          NA |
 Provider_Shorthand_PropertyAccess   |  0.7881 ns | 0.0474 ns | 0.0123 ns |  0.04 |    0.00 |    2 |         - |          NA |

Benchmarks with issues:
  ProviderResolutionBenchmarks.Needlr_Reflection_GetService: ShortRun(IterationCount=5, LaunchCount=1, WarmupCount=3)
