
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
Intel Core 7 150U, 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                                  | Mean     | Error     | StdDev    | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
---------------------------------------- |---------:|----------:|----------:|------:|--------:|-----:|-------:|----------:|------------:|
 ManualDI_CreateScopeAndResolve          | 1.133 μs | 0.4450 μs | 0.1156 μs |  1.01 |    0.13 |    1 | 0.0648 |     408 B |        1.00 |
 Needlr_Reflection_CreateScopeAndResolve | 1.171 μs | 0.4815 μs | 0.1250 μs |  1.04 |    0.14 |    1 | 0.0648 |     408 B |        1.00 |
 Needlr_SourceGen_CreateScopeAndResolve  | 1.164 μs | 0.3789 μs | 0.0586 μs |  1.04 |    0.10 |    1 | 0.0648 |     408 B |        1.00 |
