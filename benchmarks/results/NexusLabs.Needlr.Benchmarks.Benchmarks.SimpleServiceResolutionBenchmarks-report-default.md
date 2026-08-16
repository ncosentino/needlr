
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
Intel Core 7 150U, 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                          | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
-------------------------------- |---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
 ManualDI_ResolveSimple          | 12.76 ns | 5.873 ns | 1.525 ns |  1.01 |    0.16 |    1 |         - |          NA |
 Needlr_Reflection_ResolveSimple | 13.45 ns | 6.133 ns | 1.593 ns |  1.07 |    0.16 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveSimple  | 10.67 ns | 2.305 ns | 0.599 ns |  0.85 |    0.10 |    1 |         - |          NA |
