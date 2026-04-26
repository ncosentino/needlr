```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                                  | Mean     | Error     | StdDev    | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|---------------------------------------- |---------:|----------:|----------:|------:|-----:|-------:|----------:|------------:|
| ManualDI_CreateScopeAndResolve          | 2.191 μs | 0.0376 μs | 0.0058 μs |  1.00 |    1 | 0.0229 |     408 B |        1.00 |
| Needlr_Reflection_CreateScopeAndResolve | 2.216 μs | 0.0190 μs | 0.0029 μs |  1.01 |    1 | 0.0229 |     408 B |        1.00 |
| Needlr_SourceGen_CreateScopeAndResolve  | 2.309 μs | 0.0167 μs | 0.0026 μs |  1.05 |    1 | 0.0229 |     408 B |        1.00 |
