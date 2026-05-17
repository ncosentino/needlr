
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.300
  [Host]   : .NET 10.0.8 (10.0.826.23019), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.8 (10.0.826.23019), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                              | Mean       | Error     | StdDev    | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
------------------------------------ |-----------:|----------:|----------:|------:|--------:|-----:|----------:|------------:|
 ManualDI_ServiceProvider_GetService | 16.9174 ns | 1.4377 ns | 0.2225 ns |  1.00 |    0.02 |    2 |         - |          NA |
 Needlr_Reflection_GetService        |         NA |        NA |        NA |     ? |       ? |    ? |        NA |           ? |
 Needlr_SourceGen_GetService         | 18.9107 ns | 0.2950 ns | 0.0766 ns |  1.12 |    0.01 |    2 |         - |          NA |
 Provider_Interface_PropertyAccess   |  0.4871 ns | 0.0209 ns | 0.0032 ns |  0.03 |    0.00 |    1 |         - |          NA |
 Provider_Shorthand_PropertyAccess   |  0.5779 ns | 0.5548 ns | 0.1441 ns |  0.03 |    0.01 |    1 |         - |          NA |

Benchmarks with issues:
  ProviderResolutionBenchmarks.Needlr_Reflection_GetService: ShortRun(IterationCount=5, LaunchCount=1, WarmupCount=3)
