```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.201
  [Host]   : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                                  | Mean     | Error     | StdDev    | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|---------------------------------------- |---------:|----------:|----------:|------:|-----:|-------:|----------:|------------:|
| ManualDI_CreateScopeAndResolve          | 2.638 μs | 0.0279 μs | 0.0043 μs |  1.00 |    1 | 0.0229 |     408 B |        1.00 |
| Needlr_Reflection_CreateScopeAndResolve | 2.632 μs | 0.0211 μs | 0.0055 μs |  1.00 |    1 | 0.0229 |     408 B |        1.00 |
| Needlr_SourceGen_CreateScopeAndResolve  | 2.956 μs | 0.0203 μs | 0.0053 μs |  1.12 |    1 | 0.0229 |     408 B |        1.00 |
