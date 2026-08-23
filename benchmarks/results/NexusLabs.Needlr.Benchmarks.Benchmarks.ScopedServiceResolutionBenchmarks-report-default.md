
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                                  | Mean     | Error     | StdDev    | Ratio | Rank | Gen0   | Allocated | Alloc Ratio |
---------------------------------------- |---------:|----------:|----------:|------:|-----:|-------:|----------:|------------:|
 ManualDI_CreateScopeAndResolve          | 2.154 μs | 0.0071 μs | 0.0011 μs |  1.00 |    1 | 0.0229 |     408 B |        1.00 |
 Needlr_Reflection_CreateScopeAndResolve | 2.145 μs | 0.0086 μs | 0.0013 μs |  1.00 |    1 | 0.0229 |     408 B |        1.00 |
 Needlr_SourceGen_CreateScopeAndResolve  | 2.276 μs | 0.0123 μs | 0.0032 μs |  1.06 |    1 | 0.0229 |     408 B |        1.00 |
