
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                           | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
--------------------------------- |---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
 ManualDI_ResolveOptions          | 16.95 ns | 0.115 ns | 0.018 ns |  1.00 |    0.00 |    1 |         - |          NA |
 Needlr_Reflection_ResolveOptions | 16.71 ns | 0.044 ns | 0.011 ns |  0.99 |    0.00 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveOptions  | 17.34 ns | 2.642 ns | 0.686 ns |  1.02 |    0.04 |    1 |         - |          NA |
