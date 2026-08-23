
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.302
  [Host]   : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                          | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
-------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
 ManualDI_ResolveSimple          | 15.15 ns | 0.043 ns | 0.007 ns |  1.00 |    1 |         - |          NA |
 Needlr_Reflection_ResolveSimple | 14.85 ns | 0.647 ns | 0.168 ns |  0.98 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveSimple  | 15.22 ns | 0.034 ns | 0.005 ns |  1.00 |    1 |         - |          NA |
