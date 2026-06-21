
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                              | Mean       | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
------------------------------------ |-----------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
 ManualDI_ServiceProvider_GetService | 15.0193 ns | 0.0165 ns | 0.0025 ns |  1.00 |    0.00 |    2 |         - |          NA |
 Needlr_Reflection_GetService        |         NA |        NA |        NA |     ? |       ? |    ? |        NA |           ? |
 Needlr_SourceGen_GetService         | 15.0347 ns | 0.0701 ns | 0.0108 ns |  1.00 |    0.00 |    2 |         - |          NA |
 Provider_Interface_PropertyAccess   |  1.0213 ns | 0.0299 ns | 0.0046 ns |  0.07 |    0.00 |    1 |         - |          NA |
 Provider_Shorthand_PropertyAccess   |  0.9870 ns | 0.0683 ns | 0.0106 ns |  0.07 |    0.00 |    1 |         - |          NA |

Benchmarks with issues:
  ProviderResolutionBenchmarks.Needlr_Reflection_GetService: ShortRun(IterationCount=5, LaunchCount=1, WarmupCount=3)
