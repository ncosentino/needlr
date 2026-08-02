
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
12th Gen Intel Core i7-12700H, 1 CPU, 20 logical and 10 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                             | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
----------------------------------- |---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
 ManualDI_ResolveDependent          | 11.75 ns | 4.326 ns | 0.670 ns |  1.00 |    0.07 |    1 |         - |          NA |
 Needlr_Reflection_ResolveDependent | 11.03 ns | 0.823 ns | 0.127 ns |  0.94 |    0.05 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveDependent  | 11.17 ns | 2.441 ns | 0.634 ns |  0.95 |    0.07 |    1 |         - |          NA |
