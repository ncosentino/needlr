
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                          | Mean     | Error    | StdDev   | Ratio | RatioSD | Rank | Allocated | Alloc Ratio |
-------------------------------- |---------:|---------:|---------:|------:|--------:|-----:|----------:|------------:|
 ManualDI_ResolveSimple          | 19.14 ns | 0.079 ns | 0.012 ns |  1.00 |    0.00 |    1 |         - |          NA |
 Needlr_Reflection_ResolveSimple | 19.16 ns | 0.070 ns | 0.018 ns |  1.00 |    0.00 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveSimple  | 19.59 ns | 1.259 ns | 0.327 ns |  1.02 |    0.02 |    1 |         - |          NA |
