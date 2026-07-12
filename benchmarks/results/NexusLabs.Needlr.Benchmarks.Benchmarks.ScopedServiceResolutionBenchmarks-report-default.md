
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                                  | Mean     | Error     | StdDev    | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
---------------------------------------- |---------:|----------:|----------:|------:|-----:|-------:|----------:|------------:|
 ManualDI_CreateScopeAndResolve          | 2.276 μs | 0.0220 μs | 0.0057 μs |  1.00 |    1 | 0.0229 |     408 B |        1.00 |
 Needlr_Reflection_CreateScopeAndResolve | 2.253 μs | 0.0224 μs | 0.0058 μs |  0.99 |    1 | 0.0229 |     408 B |        1.00 |
 Needlr_SourceGen_CreateScopeAndResolve  | 2.370 μs | 0.0251 μs | 0.0065 μs |  1.04 |    1 | 0.0229 |     408 B |        1.00 |
