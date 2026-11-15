
BenchmarkDotNet v0.14.0, Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.404
  [Host]   : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.15 (10.0.1526.53008), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                           | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
--------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
 ManualDI_ResolveOptions          | 16.83 ns | 0.393 ns | 0.102 ns |  1.00 |    1 |         - |          NA |
 Needlr_Reflection_ResolveOptions | 17.91 ns | 0.494 ns | 0.128 ns |  1.06 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveOptions  | 16.70 ns | 0.210 ns | 0.032 ns |  0.99 |    1 |         - |          NA |
