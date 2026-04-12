
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.201
  [Host]   : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                             | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
----------------------------------- |---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
 ManualDI_ResolveDependent          | 17.30 ns | 0.200 ns | 0.052 ns |  1.00 |    0.00 |    1 |         - |          NA |
 Needlr_Reflection_ResolveDependent | 20.34 ns | 2.367 ns | 0.366 ns |  1.18 |    0.02 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveDependent  | 19.20 ns | 0.304 ns | 0.079 ns |  1.11 |    0.01 |    1 |         - |          NA |
