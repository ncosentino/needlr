```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                                  | Mean     | Error     | StdDev    | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|---------------------------------------- |---------:|----------:|----------:|------:|-----:|-------:|----------:|------------:|
| ManualDI_CreateScopeAndResolve          | 2.647 μs | 0.0958 μs | 0.0249 μs |  1.00 |    1 | 0.0229 |     408 B |        1.00 |
| Needlr_Reflection_CreateScopeAndResolve | 2.644 μs | 0.0153 μs | 0.0024 μs |  1.00 |    1 | 0.0229 |     408 B |        1.00 |
| Needlr_SourceGen_CreateScopeAndResolve  | 2.955 μs | 0.0308 μs | 0.0048 μs |  1.12 |    1 | 0.0229 |     408 B |        1.00 |
