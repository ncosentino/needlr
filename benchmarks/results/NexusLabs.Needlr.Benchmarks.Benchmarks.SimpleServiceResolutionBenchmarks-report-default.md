
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.301
  [Host]   : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                          | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
-------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
 ManualDI_ResolveSimple          | 14.60 ns | 0.159 ns | 0.041 ns |  1.00 |    1 |         - |          NA |
 Needlr_Reflection_ResolveSimple | 14.95 ns | 0.416 ns | 0.108 ns |  1.02 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveSimple  | 15.03 ns | 0.076 ns | 0.020 ns |  1.03 |    1 |         - |          NA |
