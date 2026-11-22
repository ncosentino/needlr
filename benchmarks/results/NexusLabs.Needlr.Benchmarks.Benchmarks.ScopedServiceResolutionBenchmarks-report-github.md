```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.404
  [Host]   : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  ShortRun : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                                  | Mean     | Error     | StdDev    | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|---------------------------------------- |---------:|----------:|----------:|------:|-----:|-------:|----------:|------------:|
| ManualDI_CreateScopeAndResolve          | 2.080 μs | 0.1081 μs | 0.0167 μs |  1.00 |    1 | 0.0229 |     408 B |        1.00 |
| Needlr_Reflection_CreateScopeAndResolve | 2.102 μs | 0.0896 μs | 0.0233 μs |  1.01 |    1 | 0.0229 |     408 B |        1.00 |
| Needlr_SourceGen_CreateScopeAndResolve  | 2.324 μs | 0.0200 μs | 0.0052 μs |  1.12 |    1 | 0.0229 |     408 B |        1.00 |
