
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
Intel Core 7 150U, 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                           | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
--------------------------------- |---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
 ManualDI_ResolveOptions          | 11.63 ns | 0.611 ns | 0.159 ns |  1.00 |    0.02 |    1 |         - |          NA |
 Needlr_Reflection_ResolveOptions | 12.16 ns | 4.216 ns | 0.653 ns |  1.05 |    0.05 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveOptions  | 11.53 ns | 1.481 ns | 0.385 ns |  0.99 |    0.03 |    1 |         - |          NA |
