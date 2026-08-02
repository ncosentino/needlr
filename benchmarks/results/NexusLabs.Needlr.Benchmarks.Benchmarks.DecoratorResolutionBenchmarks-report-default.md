
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
12th Gen Intel Core i7-12700H, 1 CPU, 20 logical and 10 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                             | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
----------------------------------- |---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
 ManualDI_ResolveDecorated          | 11.68 ns | 1.000 ns | 0.260 ns |  1.00 |    0.03 |    1 |         - |          NA |
 Needlr_Reflection_ResolveDecorated | 11.18 ns | 2.856 ns | 0.442 ns |  0.96 |    0.04 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveDecorated  | 11.35 ns | 2.548 ns | 0.662 ns |  0.97 |    0.06 |    1 |         - |          NA |
