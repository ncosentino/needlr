
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                                  | Mean     | Error     | StdDev    | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
---------------------------------------- |---------:|----------:|----------:|------:|-----:|-------:|----------:|------------:|
 ManualDI_CreateScopeAndResolve          | 2.748 μs | 0.0418 μs | 0.0109 μs |  1.00 |    1 | 0.0229 |     408 B |        1.00 |
 Needlr_Reflection_CreateScopeAndResolve | 2.709 μs | 0.0600 μs | 0.0093 μs |  0.99 |    1 | 0.0229 |     408 B |        1.00 |
 Needlr_SourceGen_CreateScopeAndResolve  | 3.172 μs | 0.1063 μs | 0.0276 μs |  1.15 |    1 | 0.0229 |     408 B |        1.00 |
