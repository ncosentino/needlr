```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
12th Gen Intel Core i7-12700H, 1 CPU, 20 logical and 10 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                                  | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|---------------------------------------- |---------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
| ManualDI_CreateScopeAndResolve          | 1.107 μs | 0.0984 μs | 0.0256 μs |  1.00 |    0.03 |    1 | 0.0324 |     408 B |        1.00 |
| Needlr_Reflection_CreateScopeAndResolve | 1.110 μs | 0.0981 μs | 0.0255 μs |  1.00 |    0.03 |    1 | 0.0324 |     408 B |        1.00 |
| Needlr_SourceGen_CreateScopeAndResolve  | 1.290 μs | 0.1114 μs | 0.0289 μs |  1.17 |    0.03 |    1 | 0.0324 |     408 B |        1.00 |
