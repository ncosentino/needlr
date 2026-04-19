
BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.202
  [Host]   : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.6 (10.0.626.17701), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=5  LaunchCount=1  
WarmupCount=3  

 Method                          | Mean     | Error    | StdDev   | Ratio | Rank | Allocated | Alloc Ratio |
-------------------------------- |---------:|---------:|---------:|------:|-----:|----------:|------------:|
 ManualDI_ResolveSimple          | 19.44 ns | 0.652 ns | 0.169 ns |  1.00 |    1 |         - |          NA |
 Needlr_Reflection_ResolveSimple | 19.50 ns | 0.185 ns | 0.048 ns |  1.00 |    1 |         - |          NA |
 Needlr_SourceGen_ResolveSimple  | 18.97 ns | 0.173 ns | 0.027 ns |  0.98 |    1 |         - |          NA |
