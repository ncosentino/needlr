```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

```
| Method                                  | Mean     | Error     | StdDev    | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
|---------------------------------------- |---------:|----------:|----------:|------:|-----:|-------:|----------:|------------:|
| ManualDI_CreateScopeAndResolve          | 2.158 μs | 0.0230 μs | 0.0060 μs |  1.00 |    1 | 0.0229 |     408 B |        1.00 |
| Needlr_Reflection_CreateScopeAndResolve | 2.164 μs | 0.0183 μs | 0.0028 μs |  1.00 |    1 | 0.0229 |     408 B |        1.00 |
| Needlr_SourceGen_CreateScopeAndResolve  | 2.279 μs | 0.0389 μs | 0.0101 μs |  1.06 |    1 | 0.0229 |     408 B |        1.00 |
