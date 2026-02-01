```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.2 (10.0.225.61305), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method     | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
|----------- |---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
| Reflection |       NA |       NA |       NA |     ? |       ? |    ? |        NA |           ? |
| SourceGen  | 21.98 ns | 0.134 ns | 0.021 ns |     ? |       ? |    1 |         - |           ? |

Benchmarks with issues:
  KeyedServiceResolutionBenchmarks.Reflection: ShortRun(IterationCount=5, LaunchCount=1, WarmupCount=3)
