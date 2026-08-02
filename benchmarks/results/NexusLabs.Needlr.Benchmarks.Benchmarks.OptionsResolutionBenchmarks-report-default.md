
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
12th Gen Intel Core i7-12700H, 1 CPU, 20 logical and 10 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.10 (10.0.1026.32716), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                           | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
--------------------------------- |---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
 ManualDI_ResolveOptions          | 13.47 ns | 2.869 ns | 0.745 ns |  1.00 |    0.07 |    1 |         - |          NA |
 Needlr_Reflection_ResolveOptions | 11.88 ns | 1.777 ns | 0.275 ns |  0.88 |    0.05 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveOptions  | 13.44 ns | 6.077 ns | 1.578 ns |  1.00 |    0.12 |    1 |         - |          NA |
