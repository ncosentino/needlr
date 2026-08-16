
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
Intel Core 7 150U, 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                             | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
----------------------------------- |---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
 ManualDI_ResolveDecorated          | 18.57 ns | 8.479 ns | 2.202 ns |  1.01 |    0.16 |    3 |         - |          NA |
 Needlr_Reflection_ResolveDecorated | 12.86 ns | 5.983 ns | 1.554 ns |  0.70 |    0.11 |    2 |         - |          NA |
 Needlr_SourceGen_ResolveDecorated  | 10.25 ns | 3.309 ns | 0.859 ns |  0.56 |    0.08 |    1 |         - |          NA |
