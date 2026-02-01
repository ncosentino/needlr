
BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                      | Mean | Error | Ratio | RatioSD | Rank | Alloc Ratio |
---------------------------- |-----:|------:|------:|--------:|-----:|------------:|
 BuildHost_Reflection        |   NA |    NA |     ? |       ? |    ? |           ? |
 BuildHost_SourceGen         |   NA |    NA |     ? |       ? |    ? |           ? |
 BuildHost_SourceGenExplicit |   NA |    NA |     ? |       ? |    ? |           ? |

Benchmarks with issues:
  HostBuildBenchmarks.BuildHost_Reflection: ShortRun(IterationCount=5, LaunchCount=1, WarmupCount=3)
  HostBuildBenchmarks.BuildHost_SourceGen: ShortRun(IterationCount=5, LaunchCount=1, WarmupCount=3)
  HostBuildBenchmarks.BuildHost_SourceGenExplicit: ShortRun(IterationCount=5, LaunchCount=1, WarmupCount=3)
